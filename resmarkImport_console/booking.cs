using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace resmarkImport_console
{
    class booking
    {
        public int id;
        public int res_num;
        public DateTime res_date;
        public string res_guest;
        public string res_pickup;
        public string res_product;
        public int res_a;
        public int res_y;
        public int res_u;
        public string res_comments;
        public int product_number;

        public string firstname
        {
            get {
                if (res_guest != "")
                {
                    string[] s = res_guest.Split(',');
                    return s[1];
                }
                else
                    return "";
            }
        }
        public string lastname
        {
            get
            {
                if (res_guest != "")
                {
                    string[] s = res_guest.Split(',');
                    return s[0];
                }
                else
                    return "";
            }
        }

        public booking()
        {

        }


        public string available_item()
        {
            string avail_item = "";
            SqlConnection sqlcon = new SqlConnection("Data Source=10.206.100.104\\devapps;Initial Catalog=resmark_import;user=resmark;password=7mmT@XAy;Connection Timeout=5");

            try
            {
                string sql = "";
                sqlcon.Open();

                // need to match product ID as well
                sql = "select * from tbl_product_items where datepart(yyyy,available_datetime)=datepart(yyyy,'" + res_date + "') and datepart(MM,available_datetime)=datepart(MM,'" + res_date + "') and datepart(dd,available_datetime)=datepart(dd,'" + res_date + "') and product_num='"+ product_number + "'";
                SqlCommand dbcom = new SqlCommand(sql, sqlcon);
                SqlDataReader dbread=  dbcom.ExecuteReader();

                while (dbread.Read())
                {
                    avail_item = (string)dbread["activity_id"];

                }
                dbread.Close();
            }
            finally
            {
                sqlcon.Dispose();
            }

            return avail_item;
        }

        public string update_status(string errorMessage,bool migrated,string order_id="",int confirmation_no=-1)
        {
            string avail_item = "";
            SqlConnection sqlcon = new SqlConnection("Data Source=10.206.100.104\\devapps;Initial Catalog=resmark_import;user=resmark;password=7mmT@XAy;Connection Timeout=5");

            try
            {
                string sql = "";
                sqlcon.Open();

                if (errorMessage == null)
                    errorMessage = "";

                sql = "update tbl_to_import set status_response='" + errorMessage.Replace("'","''") + "',status_migrate='" + migrated +"',order_id='"+ order_id + "',confirmation="+confirmation_no + " where id=" + id;
                SqlCommand dbcom = new SqlCommand(sql, sqlcon);
                dbcom.ExecuteNonQuery();
            }
            finally
            {
                sqlcon.Dispose();
            }

            return avail_item;
        }
        public List<booking> get_bookings(bool ignore_fail)
        {
            List<booking> booking_list = new List<booking>();

            SqlConnection sqlcon = new SqlConnection("Data Source=10.206.100.104\\devapps;Initial Catalog=resmark_import;user=resmark;password=7mmT@XAy;Connection Timeout=5");

            try
            {

                sqlcon.Open();
                string sql = "select top 50 * from view_to_import where res_pickup is NOT NULL and status_migrate='false' and res_date> '"+ DateTime.Today + "' and (res_guest IS NOT NULL) order by res_date asc";
                if (ignore_fail)
                    sql = "select top 50 * from view_to_import where res_pickup is NOT NULL and status_migrate='false' and status_response !='' and res_date> '" + DateTime.Today + "' and (res_guest IS NOT NULL) order by res_date asc";
                SqlCommand dbcom = new SqlCommand(sql, sqlcon);

                SqlDataReader dbread = dbcom.ExecuteReader();
                while (dbread.Read())
                {
                    booking tmp = new booking();
                    tmp.id = Convert.ToInt32(dbread["id"]);
                    tmp.res_num = Convert.ToInt32(dbread["res_num"]);
                    tmp.res_date = Convert.ToDateTime(dbread["res_date"]);
                    tmp.res_guest = Convert.ToString(dbread["res_guest"]==DBNull.Value?"": dbread["res_guest"]);
                    tmp.res_pickup = Convert.ToString(dbread["res_pickup"]);
                    tmp.res_product = Convert.ToString(dbread["res_item"]);
                    tmp.res_a = Convert.ToInt32(dbread["res_A"]);
                    tmp.res_y = Convert.ToInt32(dbread["res_Y"]);
                    tmp.res_u = Convert.ToInt32(dbread["res_U"]);
                    tmp.res_comments = Convert.ToString(dbread["res_Comments"]);
                    tmp.product_number = new product().get_product_num(tmp.res_product.Replace("'", "''"));
                    booking_list.Add(tmp);
                }
                dbread.Close();
            }
            finally
            {
                sqlcon.Dispose();
            }


            return booking_list;
        }
    }
}
