using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace resmarkImport_console
{
    class Program
    {
        static string url = "";
        // static string apikey= "fd5323c3-f2cc-4364-a27c-0c7144d37c4b"; // bootIt api key
        // static string username = "info2@islandroutes.com";

        static string apikey = "ba20f0dd-95b4-4544-965d-a22515b729e2";// "48a1b104-1dc8-4ab6-8564-b7e262c03d2d"; // bootIt api key
        static string username = "carol.menzel@jetblue.com";
        static string token;
        static Hashtable pickup_lookup;

        static async Task Main(string[] args)
        {
            Console.WriteLine("Username: " + username);
            Console.WriteLine("API Key: " + apikey);
            Console.WriteLine("Action: Authenticating.... Sending API Keys....");
            token= await Auth();
            if (token.Length > 2)
            {
                Console.WriteLine("Result: API Token Received...");
                Console.WriteLine();
                // dynamic json=await get_product("588a65d9621edaec1a1b70d6");
               
                //deserialize json

            }
            Console.WriteLine("1. Download (New Resmark) Products");
            Console.WriteLine("2. ####");
            Console.WriteLine("3. Execute -> Resmark Data Migration");
            Console.WriteLine("4. Download -> Get All Product Items (Inventory)");
            Console.WriteLine("5. Execute -> Resmark Data Migration Ignore Failures");
            Console.WriteLine("6. Download -> Get All Product Supplier");


            input: Console.Write(">>"); string keyv= Console.ReadLine();

            if (keyv=="1")
            {
                dynamic json = await load_products();
                import_to_db(json);
                goto input;
            }
            else if (keyv == "3")
            {
               await begin_integrate(false);

                //dynamic json = await get_product_items("588a65d9621edaec1a1b70d6","2019-12-11","2019-12-11");
                //Console.WriteLine(json);
                goto input;
            }
            else if (keyv == "5")
            {
                await begin_integrate(true);

                //dynamic json = await get_product_items("588a65d9621edaec1a1b70d6","2019-12-11","2019-12-11");
                //Console.WriteLine(json);
                goto input;
            }
            else if (keyv == "4")
            {
                Console.WriteLine("Begin Inventory Download-->");
                await pull_product_items();

                //dynamic json = await get_product_items("588a65d9621edaec1a1b70d6","2019-12-11","2019-12-11");

                goto input;
            }
            else if (keyv == "6")
            {
                Console.WriteLine("Begin Pickup Download-->");
                await pull_product_supplier();

                //dynamic json = await get_product_items("588a65d9621edaec1a1b70d6","2019-12-11","2019-12-11");

                goto input;
            }
            else
            {
                Console.WriteLine("Invalid option"); goto input;
            }

        }

        static async Task begin_integrate(bool ignore_fail)
        {

            //build product matching table
            product prod = new product();
            Hashtable product_lookup = prod.product_lookup();

            //build pickup matching table          
             pickup_lookup = prod.pickup_lookup();

            //load products to be imported
            booking bookclass = new booking();
            List <booking> bookings= bookclass.get_bookings(ignore_fail);int x = 1;
            foreach (booking mbook in bookings)
            {
                Console.Write(x + ". Migrating Resmark#: " + mbook.res_num + " -> ");x++;
                if (mbook.available_item().Length > 0)
                {
                    //item available
                    //assign activity ID
                    string activity_id = mbook.available_item();

                    //get pickup ID
                    pickup this_pickup= await get_pickup_details(mbook);

                    //does pick up location match??
                    if (this_pickup!=null)
                    {
                        //pickup location found
                        //create cart item
                        string cart_id = await create_cart();

                        if (cart_id.Length > 0)
                        {
                            //add item to cart
                            return_object cart_return = await add_item_to_cart(activity_id, cart_id, mbook, this_pickup);
                            mbook.update_status(cart_return.return_message, false); //update booking record with response after add item to cart
                            if (cart_return.return_obj != null)//no errors. next step
                            {
                                //Lets add customer details
                                JObject jObject = (JObject)cart_return.return_obj;
                                return_object cart_update_return = await add_customer_info(cart_id, mbook);
                                mbook.update_status(cart_return.return_message, false); //update booking record with response after add customer

                                if (cart_update_return.return_obj != null)//no errors from add customer. next step
                                {
                                    return_object create_order_return = await create_order(cart_id, mbook); // create order
                                    if (create_order_return.return_obj != null)//no errors from add customer. next step
                                    {
                                        JObject jobject = (JObject)create_order_return.return_obj;
                                        Console.WriteLine("Migration completed > Confirmation: " + (string)(jobject["confirmation"]));
                                        mbook.update_status(create_order_return.return_message, true, (string)jobject["id"], Convert.ToInt32(jobject["confirmation"])); //update booking record with response after successful create order
                                    }
                                    else
                                    {
                                        Console.WriteLine("Error : " + create_order_return.return_message);
                                        mbook.update_status(create_order_return.return_message, false); //update booking record with response after failed create order
                                    }
                                }else
                                {
                                    //error adding customer to cart
                                    Console.WriteLine("Error adding customer info to cart");
                                    mbook.update_status("Error adding customer info to cart", false);
                                }
                            }
                           else {
                                // error adding item to cart
                                Console.WriteLine("Error adding item to cart ");
                              //  mbook.update_status("Error adding item to cart", false);
                            }
                        }
                        else {
                            //no cart id returned
                            Console.WriteLine("No cart id returned");
                            mbook.update_status("No cart id", false);
                        }

                    }//no pickup options found.. what to do next?

                }//no date available, update booking record
                else
                {
                    Console.WriteLine("No available dates found");
                    mbook.update_status("No available dates", false);
                }

                   
            }
        }

       public struct return_object
        {
           public string return_message;
           public dynamic return_obj;
        }

        static async Task<return_object> create_order(string cart_id,booking this_booking)
        {
            string ret = "";
            return_object returnObject = new return_object();

            dynamic jsonObj = "";
            string json_body = @"{""id"":""" + cart_id + @""",""billing"":{},""orderId"":null,""referenceId"":""test comment""}";
            string _url = "https://app.resmarksystems.com/public/api/order";
            HttpClient _client = new HttpClient();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            _client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            var response = await _client.PostAsync(_url, new StringContent(json_body, Encoding.UTF8, "application/json"));

            try
            {
                // response.EnsureSuccessStatusCode();

                ret = await response.Content.ReadAsStringAsync();

                JavaScriptSerializer serializer = new JavaScriptSerializer();
                jsonObj = serializer.Deserialize<dynamic>(ret);
                JObject jObject = JObject.Parse(ret);
                // jObject.

                returnObject.return_message = (string)jObject["errorMessage"];
                returnObject.return_obj = (JObject)jObject["data"];

                //ret = (string)data["id"];
                // var t = cart_id["id"];
            }
            catch (Exception ex)
            {
                Console.WriteLine("Create Order Exception: " + ex.Message);
            }

            return returnObject;
        }

        static string get_flight(string comment_string)
        {
            string flight_num = "";

            try
            {
                Regex regex = new Regex(@"\[.*?\]");
                MatchCollection matches = regex.Matches(comment_string);
                int count = matches.Count;

                if (count>0)
                {

                    string unparse = matches[0].Value;
                    unparse = unparse.Remove(unparse.IndexOf(']')).Substring(unparse.IndexOf('[') + 1);
                    
                    string[] s = unparse.Split(',');
                    flight_num = s[0];

                }


            }
            catch (Exception ex)
            {

            }
            return flight_num;
        }

        static async Task<return_object> add_customer_info(string cart_id, booking this_booking)
        {
            string ret = "";
            return_object returnObject = new return_object();

            string flight_num = get_flight(this_booking.res_comments);//extract flight number
           // flight_num = "AA875";
            dynamic jsonObj = "";
            string json_body = @"{""email"":""info@islandroutes.com"",""firstName"":""" + this_booking.firstname + @""",""lastName"":""" + this_booking.lastname + @""",""title"":""title"",""phone"":""8777688370"",""organization"":""book it"",""postalCode"":""00000"",""state"":""FL"",""country"":""USA"",""Arrival Flight Number"":""" + flight_num + @""",""Departure Airline and Flight Number"":""0000""}";
            string _url = "https://app.resmarksystems.com/public/api/cart/" + cart_id + "/customer";
            HttpClient _client = new HttpClient();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            _client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            var response = await _client.PutAsync(_url, new StringContent(json_body, Encoding.UTF8, "application/json"));

            try
            {
                // response.EnsureSuccessStatusCode();

                ret = await response.Content.ReadAsStringAsync();

                JavaScriptSerializer serializer = new JavaScriptSerializer();
                jsonObj = serializer.Deserialize<dynamic>(ret);
                JObject jObject = JObject.Parse(ret);
                // jObject.

                returnObject.return_message = (string)jObject["errorMessage"];
                returnObject.return_obj = (JObject)jObject["data"];

                //ret = (string)data["id"];
                // var t = cart_id["id"];
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.Message);
            }

            return returnObject;
        }
        static async Task<return_object> add_item_to_cart(string activity_id,string cart_id,booking this_booking,pickup pickup_d)
        {
            string ret = "";
            return_object returnObject = new return_object();

            //string flight_num = get_flight(this_booking.res_comments);

            dynamic jsonObj = "";

            List<string> price_tiers = new List<string>();

            try
            {
               price_tiers = new product().get_tiers(activity_id);
            }
            catch (Exception e)
            {

            }

            string json_body = "";

           

            try
            {

                if (price_tiers.Count > 1)
                {
                    //probably aduly/child
                    json_body = @"{""itemId"":""" + activity_id + @""",""participants"":{""All"":""" + 1 + @""",""" + price_tiers[0] + @""":""" + this_booking.res_a + @""",""" + price_tiers[1] + @""":""" + this_booking.res_y + @"""},""locationId"":""" + pickup_d.location_id + @""",""pickupDetailId"":""" + pickup_d.pickup_detail_id + @"""}";

                }
                else
                {
                    if (price_tiers[0]=="Adult") //use Adult
                        json_body = @"{""itemId"":""" + activity_id + @""",""participants"":{""All"":""" + 1 + @""",""" + price_tiers[0] + @""":""" + this_booking.res_a + @"""},""locationId"":""" + pickup_d.location_id + @""",""pickupDetailId"":""" + pickup_d.pickup_detail_id + @"""}";
                     else
                    //single tier,, use Unit
                    json_body = @"{""itemId"":""" + activity_id + @""",""participants"":{""All"":""" + 1 + @""",""" + price_tiers[0] + @""":""" + this_booking.res_u + @"""},""locationId"":""" + pickup_d.location_id + @""",""pickupDetailId"":""" + pickup_d.pickup_detail_id + @"""}";
                }


                string _url = "https://app.resmarksystems.com/public/api/cart/" + cart_id + "/item";
                HttpClient _client = new HttpClient();
                _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                _client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                var response = await _client.PostAsync(_url, new StringContent(json_body, Encoding.UTF8, "application/json"));

                // response.EnsureSuccessStatusCode();

                ret = await response.Content.ReadAsStringAsync();

                JavaScriptSerializer serializer = new JavaScriptSerializer();
                jsonObj = serializer.Deserialize<dynamic>(ret);
                JObject jObject = JObject.Parse(ret);
                // jObject.

                returnObject.return_message = (string)jObject["errorMessage"];
                returnObject.return_obj= (JObject)jObject["data"];

            }
            catch (Exception ex)
            {
               // Console.WriteLine("Add to Cart Exception: " + ex.Message);
            }

            return returnObject;
        }

        static async Task<string> create_cart()
        {
            string ret = "";
            dynamic jsonObj = "";
            string json_body = @"{}";
            string _url = "https://app.resmarksystems.com/public/api/cart";
            HttpClient _client = new HttpClient();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            _client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            var response = await _client.PostAsync(_url, new StringContent(json_body, Encoding.UTF8, "application/json"));

            try
            {
                response.EnsureSuccessStatusCode();

                ret = await response.Content.ReadAsStringAsync();

                JavaScriptSerializer serializer = new JavaScriptSerializer();
                jsonObj = serializer.Deserialize<dynamic>(ret);
                JObject jObject = JObject.Parse(ret);
                // jObject.

                JObject data = (JObject)jObject["data"];

                ret = (string)data["id"];
               // var t = cart_id["id"];
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.Message);
            }

            return ret;
        }

        static async Task pull_product_items()
        {

            product prod = new product();
           foreach (int prod_num in prod.get_products())
            {
               prod.delete_items(prod_num);
               await get_items(prod_num);
            }
           
        }

        static async Task pull_product_supplier()
        {

            product prod = new product();
            foreach (int prod_num in prod.get_raw_products())
            {
                //  prod.delete_items(prod_num);
                //   await get_supplier(prod_num);
                Console.WriteLine("Working Product: " + prod_num);
                pickup this_pickup = await get_supplier_details(prod_num);
            }

        }

        static async Task<pickup> get_supplier_details(int prod_num)
        {
            dynamic jsonObj = "";
            string ret = "";
            pickup pickup_d = new pickup();

            string _url = "https://app.resmarksystems.com/public/api/product/" + prod_num + "/pickup";
            HttpClient _client = new HttpClient();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            _client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            var response = await _client.GetAsync(_url);

            try
            {
                //  response.EnsureSuccessStatusCode();

                ret = await response.Content.ReadAsStringAsync();

                JavaScriptSerializer serializer = new JavaScriptSerializer();
                jsonObj = serializer.Deserialize<dynamic>(ret);
                JObject jObject = JObject.Parse(ret);
               
                JArray data = (JArray)jObject["data"];

                // var vals = data.SelectTokens("$..locations[?(@.name=='" + this_book.res_pickup.Replace("'", "\\'") + "')]");
                // JToken pickup_location = data.SelectToken("$..locations[?(@.name=='" + this_book.res_pickup.Replace("'", "\\'") + "')]");

                try
                {

                  //  JArray items = (JArray)data["items"];


                    foreach (var item in data)
                    {
                       
                        product mproduct = new product();
                        string zone_id = (string)item["id"];
                        string zone_name = (string)item["name"];

                        JArray pickup_data = (JArray)item["pickupDetails"];
                        Console.Write(" -> getting PickupDetails");
                        foreach (var pickup in pickup_data)
                        {
                            mproduct.add_supplier_details(prod_num, (string)pickup["name"], zone_id, zone_name);
                        }

                    }
                        // pickup_d.location_id = (string)(pickup_location["id"]);
                        // JToken pickup_detail = pickup_location.Parent.Parent.Parent.SelectToken("pickupDetails[0]");
                        // if ((string)(pickup_detail["id"]) != null)
                        //   pickup_d.pickup_detail_id = (string)(pickup_detail["id"]);
                    }
                catch (Exception ex)
                {
                    pickup_d.pickup_detail_id = "";
                    pickup_d.location_id = "";
                    Console.WriteLine("Pick up Exception: " + ex.Message);
                }

            }


            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.Message);
            }

            return pickup_d;
        }

        static async Task<pickup> get_pickup_details(booking this_book)
        {
            dynamic jsonObj = "";
            string ret = "";
            pickup pickup_d = new pickup();

            string _url = "https://app.resmarksystems.com/public/api/product/" + this_book.product_number + "/pickup";
            HttpClient _client = new HttpClient();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            _client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            var response = await _client.GetAsync(_url);

            try
            {
              //  response.EnsureSuccessStatusCode();

                ret = await response.Content.ReadAsStringAsync();

                JavaScriptSerializer serializer = new JavaScriptSerializer();
                jsonObj = serializer.Deserialize<dynamic>(ret);
                JObject jObject = JObject.Parse(ret);
                // jObject.

                JArray data = (JArray)jObject["data"];

                // JToken pickup_location = data.SelectToken("$..locations[?(@.name=='" + pickup_lookup[this_book.res_pickup].ToString() + "')]");
                var vals =data.SelectTokens("$..locations[?(@.name=='" + this_book.res_pickup.Replace("'", "\\'") + "')]");
                JToken pickup_location = data.SelectToken("$..locations[?(@.name=='" +this_book.res_pickup.Replace("'","\\'") + "')]");

                try
                {
                    pickup_d.location_id = (string)(pickup_location["id"]);
                    JToken pickup_detail = pickup_location.Parent.Parent.Parent.SelectToken("pickupDetails[0]");
                    if ((string)(pickup_detail["id"]) != null)
                        pickup_d.pickup_detail_id = (string)(pickup_detail["id"]);
                }catch (Exception ex)
                {
                    pickup_d.pickup_detail_id = "";
                    pickup_d.location_id = "";

                }
               
              //  var het=pickup_detail.Value<string>("pickupDetails");

              
               

               
                //    var v = pickup_detail.Value<string>("pickupDetails");
                // foreach (var item in items)
                {
                 //   dynamic item_date = item["date"];
                    //get timesAvalable    
                  //  JArray timesAvail = (JArray)(item["timesAvailable"]);
                  //  new product().add_pickup_items(productNum, timesAvail);
                    //   datetime
                }

               
            }

          
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.Message);
            }

            return pickup_d;
        }

        static async Task get_items(int productNum)
        {
            dynamic jsonObj = "";
            string ret = "";

           // productNum = 94175;

            string _url = "https://app.resmarksystems.com/public/api/product/" + productNum + "/item?from=2020-04-25&to=2020-12-25";
            HttpClient _client = new HttpClient();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            _client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            var response = await _client.GetAsync(_url);

            try
            {
                response.EnsureSuccessStatusCode();

                ret = await response.Content.ReadAsStringAsync();

              //  JavaScriptSerializer serializer = new JavaScriptSerializer();
              //  jsonObj = serializer.Deserialize<dynamic>(ret);
                JObject jObject = JObject.Parse(ret);
                // jObject.

                JObject data = (JObject)jObject["data"];
                JArray items = (JArray)data["items"];


                foreach (var item in items)
                {
                    dynamic item_date = item["date"];
                    //get timesAvalable    
                  JArray timesAvail = (JArray)(item["timesAvailable"]);
                    new product().add_pickup_items(productNum,timesAvail);
                    //   datetime
                }

                //  JArray a = JArray.Parse(data);
                //  string c = jsonObj["data"]["id"];
                Console.WriteLine("Download of Product(" + productNum + ") Inventory Complete");
            }
           
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.Message);
            }
        }

        static async Task get_supplier(int productNum)
        {
            dynamic jsonObj = "";
            string ret = "";

            // productNum = 94175;

            string _url = "https://app.resmarksystems.com/public/api/product/" + productNum + "/item?from=2019-12-25&to=2020-01-25";
            HttpClient _client = new HttpClient();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            _client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            var response = await _client.GetAsync(_url);

            try
            {
                response.EnsureSuccessStatusCode();

                ret = await response.Content.ReadAsStringAsync();

                //  JavaScriptSerializer serializer = new JavaScriptSerializer();
                //  jsonObj = serializer.Deserialize<dynamic>(ret);
                JObject jObject = JObject.Parse(ret);
                // jObject.

                JObject data = (JObject)jObject["data"];
                JArray items = (JArray)data["items"];


                foreach (var item in items)
                {
                    dynamic item_date = item["date"];
                    //get timesAvalable    
                    JArray timesAvail = (JArray)(item["timesAvailable"]);
                    new product().add_pickup_items(productNum, timesAvail);
                    //   datetime
                }

                //  JArray a = JArray.Parse(data);
                //  string c = jsonObj["data"]["id"];
                Console.WriteLine("Download of Product(" + productNum + ") Inventory Complete");
            }

            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.Message);
            }
        }


        static void import_to_db(dynamic json)
        {
            object[] product_data = json["data"];
            
            foreach(dynamic data in product_data)
            {
                try
                {
                    product _product = new product(data["productNumber"], data["id"], (string)data["name"]);
                    _product.add();
                }catch(Exception ex)
                {
                    Console.WriteLine("Exception: " + ex.Message + " -> " + (string)data["name"]);
                }
            }

        }

        static async Task<string> Auth()
        {
           
                url = "https://app.resmarksystems.com/public/api/authenticate";
                string json_body = @"{""username"":""" + username + @""",""apikey"":""" + apikey + @"""}";
                HttpClient client = new HttpClient();
                client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                var response = await client.PostAsync(url, new StringContent(json_body, Encoding.UTF8, "application/json"));
                // HttpResponseMessage res
                string res = response.Content.ReadAsByteArrayAsync().Result.ToString();
                
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    res = response.Content.ReadAsStringAsync().Result.ToString();
                }
                // string status = ("{0}",res);
                return res;
          
          
        }

        static async Task<dynamic> load_products()
        {
            Console.WriteLine("..... Downloading All Products");
            string ret = "";
            dynamic jsonObj = "";
            string _url = "https://app.resmarksystems.com/public/api/product?limit=200&page=1";
            HttpClient _client = new HttpClient();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            _client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            var response = await _client.GetAsync(_url);

            try
            {
                response.EnsureSuccessStatusCode();

                ret = await response.Content.ReadAsStringAsync();

                JavaScriptSerializer serializer = new JavaScriptSerializer();
                jsonObj = serializer.Deserialize<dynamic>(ret);
                //  string c = jsonObj["data"]["id"];
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.Message);
            }

            return jsonObj;
        }

        static async Task<dynamic> get_product(string product_id)
        {
            string ret="";
            dynamic jsonObj="";
            string _url = "https://app.resmarksystems.com/public/api/product/"+ product_id;
            HttpClient _client = new HttpClient();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            _client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            var response = await _client.GetAsync(_url);

            try
            {
                response.EnsureSuccessStatusCode();

                ret = await response.Content.ReadAsStringAsync();

                JavaScriptSerializer serializer = new JavaScriptSerializer();
                jsonObj = serializer.Deserialize<dynamic>(ret);
              //  string c = jsonObj["data"]["id"];
            }catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.Message);
            }

            return jsonObj;
        }

        static async Task<dynamic> get_product_items(string product_id,string date_from,string date_to)
        {
            string ret = "";
            dynamic jsonObj = "";
            string _url = "https://app.resmarksystems.com/public/api/product/" + product_id + "/item?from="+date_from + "&to="+date_to;
            HttpClient _client = new HttpClient();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            _client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            var response = await _client.GetAsync(_url);

            try
            {
                response.EnsureSuccessStatusCode();

                ret = await response.Content.ReadAsStringAsync();

                JavaScriptSerializer serializer = new JavaScriptSerializer();
                jsonObj = serializer.Deserialize<dynamic>(ret);
                //  string c = jsonObj["data"]["id"];
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.Message);
            }

            return jsonObj;
        }

        static void auth2()
        {
            url = "https://app.resmarksystems.com/public/api/authenticate";
            string json_body = @"{""username"":""roghe.deokoro@islandroutes.com"",""apikey"":""48a1b104-1dc8-4ab6-8564-b7e262c03d2d""}";

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "POST";
            request.ContentType = "application/json";
            request.ContentLength = json_body.Length;
            using (Stream webStream = request.GetRequestStream())
            using (StreamWriter requestWriter = new StreamWriter(webStream, System.Text.Encoding.ASCII))
            {
                requestWriter.Write(json_body);
            }

            try
            {
                WebResponse webResponse = request.GetResponse();
                using (Stream webStream = webResponse.GetResponseStream() ?? Stream.Null)
                using (StreamReader responseReader = new StreamReader(webStream))
                {
                    string response = responseReader.ReadToEnd();
                    Console.Out.WriteLine(response);
                }
            }
            catch (Exception e)
            {
                Console.Out.WriteLine("-----------------");
                Console.Out.WriteLine(e.Message);
            }

        }
    }
}
