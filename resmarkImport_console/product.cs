using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SqlClient;
using Newtonsoft.Json.Linq;

namespace resmarkImport_console
{
    class product
    {
        public int product_num;
        public string product_id;
        public string product_name;
        public float product_price;
        public List<string> tiers=new List<string>() ;
       
        public struct product_item
        {
            public int id;
            public string activity_id;
            public DateTime available_datetime;
        }

        public product()
        {

        }

        public product(int pnum,string prod_id, string prod_name)
        {
            product_num = pnum;
            product_id = prod_id;
            product_name = prod_name;
          //  product_price = prod_price;
        }

        public List<string> get_tiers(string activity_id)
        {
            List<string> tiers = new List<string>();

            SqlConnection sqlcon = new SqlConnection("Data Source=10.206.100.104\\devapps;Initial Catalog=resmark_import;user=resmark;password=7mmT@XAy;Connection Timeout=5");

            try
            {

                sqlcon.Open();
                string sql = "select tiers from  tbl_product_items where activity_id like '" + activity_id + "'";
                SqlCommand dbcom = new SqlCommand(sql, sqlcon);

                SqlDataReader dbread = dbcom.ExecuteReader();
                while (dbread.Read())
                {
                    string[] tierz = Convert.ToString(dbread["tiers"]).Split(',');
                    tiers = tierz.ToList<string>();
                }
                dbread.Close();

               
            }
            finally
            {
                sqlcon.Dispose();
            }


            return tiers;
        }
        public void add()
        {
            SqlConnection sqlcon = new SqlConnection("Data Source=10.206.100.104\\devapps;Initial Catalog=resmark_import;user=resmark;password=7mmT@XAy;Connection Timeout=5");

            try
            {

                sqlcon.Open();
                string sql = "insert into tbl_products (product_id,product_number,product_name) values ('" + product_id + "'," + product_num +",'" + product_name.Replace("'","''") + "')";
                SqlCommand dbcom = new SqlCommand(sql,sqlcon);
                dbcom.ExecuteNonQuery();

            }finally
            {
                sqlcon.Dispose();
            }
        }


        public void add_supplier_details(int prod_num,string supplier,string zone_id,string zone_name)
        {
            SqlConnection sqlcon = new SqlConnection("Data Source=10.206.100.104\\devapps;Initial Catalog=resmark_import;user=resmark;password=7mmT@XAy;Connection Timeout=5");

            try
            {

                sqlcon.Open();
                string sql = "insert into tbl_product_supplier (product_num,supplier,zone_id,zone_name) values (" + prod_num + ",'" + supplier + "','" + zone_id.Replace("'", "''") + "','" + zone_name.Replace("'", "''") + "')";
                SqlCommand dbcom = new SqlCommand(sql, sqlcon);
                dbcom.ExecuteNonQuery();

            }
            finally
            {
                sqlcon.Dispose();
            }
        }


        public void add_pickup_items(int productnum,JArray available_times)
        {
            SqlConnection sqlcon = new SqlConnection("Data Source=10.206.100.104\\devapps;Initial Catalog=resmark_import;user=resmark;password=7mmT@XAy;Connection Timeout=5");

            try
            {
                string sql = "";
                sqlcon.Open();

                if (available_times.Count > 0)
                {
                    // foreach (var avail in available_times)
                    JToken avail = available_times[0];
                    JToken price_tier = avail["price"];

                    Dictionary<string, string> dictObj = price_tier.ToObject<Dictionary<string, string>>();

                    {
                        string tiers = "";
                        foreach (string key in dictObj.Keys)
                        {
                            tiers += key + ",";
                        }

                        tiers = tiers.Remove(tiers.Length - 1);
                        sql = "insert into tbl_product_items (product_num,available_datetime,activity_id,tiers) values (" + productnum + ",'" + (DateTime)avail["startTime"] + "','" + (string)avail["activityId"] + "','" + tiers +"')";
                        SqlCommand dbcom = new SqlCommand(sql, sqlcon);
                        dbcom.ExecuteNonQuery();
                    }
                }

            }
            finally
            {
                sqlcon.Dispose();
            }
        }

        public  Hashtable product_lookup()
        {
            Hashtable plu = new Hashtable();

            SqlConnection sqlcon = new SqlConnection("Data Source=10.206.100.104\\devapps;Initial Catalog=resmark_import;user=resmark;password=7mmT@XAy;Connection Timeout=5");

            try
            {

                sqlcon.Open();
                string sql = "select * from tbl_product_map";
                SqlCommand dbcom = new SqlCommand(sql, sqlcon);

                SqlDataReader dbread = dbcom.ExecuteReader();
                while (dbread.Read())
                {
                    if (!plu.ContainsKey((string)dbread["old_product"]))
                      plu.Add((string)dbread["old_product"], Convert.ToString(dbread["new_product"]).Replace("'", "''"));
                }
                dbread.Close();
            }
            finally
            {
                sqlcon.Dispose();
            }

            return plu;
        }

        public int get_product_num(string prod_name)
        {
            int productno = 0;

            SqlConnection sqlcon = new SqlConnection("Data Source=10.206.100.104\\devapps;Initial Catalog=resmark_import;user=resmark;password=7mmT@XAy;Connection Timeout=5");

            try
            {

                sqlcon.Open();
                string sql = "select product_number from view_product_map where old_product like '" + prod_name +"'";
                SqlCommand dbcom = new SqlCommand(sql, sqlcon);

                SqlDataReader dbread = dbcom.ExecuteReader();
                while (dbread.Read())
                {
                    productno = Convert.ToInt32(dbread["product_number"]);
                }
                dbread.Close();
            }
            finally
            {
                sqlcon.Dispose();
            }

            return productno;
        }


        public List<int> get_raw_products()
        {
            List<int> prods = new List<int>();

            SqlConnection sqlcon = new SqlConnection("Data Source=10.206.100.104\\devapps;Initial Catalog=resmark_import;user=resmark;password=7mmT@XAy;Connection Timeout=5");

            try
            {

                sqlcon.Open();
                string sql = "SELECT DISTINCT product_number FROM tbl_products";
                SqlCommand dbcom = new SqlCommand(sql, sqlcon);

                SqlDataReader dbread = dbcom.ExecuteReader();
                while (dbread.Read())
                {
                    prods.Add(Convert.ToInt32(dbread["product_number"]));
                }
                dbread.Close();
            }
            finally
            {
                sqlcon.Dispose();
            }

            return prods; ;
        }

        public List<int> get_products()
        {
            List<int> prods = new List<int>();

            SqlConnection sqlcon = new SqlConnection("Data Source=10.206.100.104\\devapps;Initial Catalog=resmark_import;user=resmark;password=7mmT@XAy;Connection Timeout=5");

            try
            {

                sqlcon.Open();
                string sql = "SELECT DISTINCT product_number FROM View_product_map";
                SqlCommand dbcom = new SqlCommand(sql, sqlcon);

                SqlDataReader dbread = dbcom.ExecuteReader();
                while (dbread.Read())
                {
                    prods.Add(Convert.ToInt32(dbread["product_number"]));
                }
                dbread.Close();
            }
            finally
            {
                sqlcon.Dispose();
            }

            return prods; ;
        }

        public void delete_items(int prodNum)
        {
           
            SqlConnection sqlcon = new SqlConnection("Data Source=10.206.100.104\\devapps;Initial Catalog=resmark_import;user=resmark;password=7mmT@XAy;Connection Timeout=5");

            try
            {

                sqlcon.Open();
                string sql = "delete from tbl_product_items where product_num="+prodNum;
                SqlCommand dbcom = new SqlCommand(sql, sqlcon);

                dbcom.ExecuteNonQuery();
            }
            finally
            {
                sqlcon.Dispose();
            }

           
        }

        public  Hashtable pickup_lookup()
        {
            Hashtable plu = new Hashtable();

            SqlConnection sqlcon = new SqlConnection("Data Source=10.206.100.104\\devapps;Initial Catalog=resmark_import;user=resmark;password=7mmT@XAy;Connection Timeout=5");

            try
            {

                sqlcon.Open();
                string sql = "select * from tbl_pickup_map";
                SqlCommand dbcom = new SqlCommand(sql, sqlcon);

                SqlDataReader dbread = dbcom.ExecuteReader();
                while (dbread.Read())
                {
                    if (!plu.ContainsKey((string)dbread["code"]))
                      plu.Add((string)dbread["code"], (string)dbread["name"]);
                }
                dbread.Close();
            }
            finally
            {
                sqlcon.Dispose();
            }

            return plu;
        }
    }
}
