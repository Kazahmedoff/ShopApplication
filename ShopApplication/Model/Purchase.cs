using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data;
using System.Diagnostics;

namespace ShopApplication.Model
{
    public class Purchase
    {
        private int client_id;
        private Dictionary<int, int> products;

        public Purchase(int client_id, Dictionary<int, int> products)
        {
            this.client_id = client_id;
            this.products = products;
        }

        public static void ClearPurchaseTable(SqlConnection connection)
        {
            connection.Open();

            SqlCommand command = connection.CreateCommand();

            command.CommandText = @"DELETE FROM Shop.dbo.Purshases";
            command.ExecuteNonQuery();

            command.CommandText = @"DBCC CHECKIDENT ('Shop.dbo.Purshases', RESEED, 0)";
            command.ExecuteNonQuery();

            connection.Close();
        }

        public void AddToDatabase(SqlConnection connection)
        {
            connection.Open();

            SqlCommand command = connection.CreateCommand();
            command.CommandText = @"INSERT INTO Shop.dbo.Purshases (Client_ID, Product_ID, Date, Product_count, Cost)
                                        VALUES (@client_id, @product_id, @date, @product_count, @cost)";
            command.Parameters.Add(@"client_id", SqlDbType.Int);
            command.Parameters.Add(@"product_id", SqlDbType.Int);
            command.Parameters.Add(@"date", SqlDbType.DateTime);
            command.Parameters.Add("@product_count", SqlDbType.Int);
            command.Parameters.Add("@cost", SqlDbType.Float);

            SqlCommand command1 = connection.CreateCommand();

            System.DateTime date = System.DateTime.Now;
            double cost = 0;

            foreach(KeyValuePair<int, int> product in this.products)
            {
                command1.CommandText = string.Format("SELECT Cost FROM Shop.dbo.Products WHERE Product_ID = '{0}'",
                product.Key);

                using (SqlDataReader reader = command1.ExecuteReader())
                {
                    if (reader.HasRows)
                    {
                        reader.Read();
                        cost = reader.GetDouble(reader.GetOrdinal("Cost"));
                        cost = cost * product.Value;
                    }
                }

                command.CommandText = @"INSERT INTO Shop.dbo.Purshases (Client_ID, Product_ID, Date, Product_count, Cost)
                                        VALUES (@client_id, @product_id, @date, @product_count, @cost)";
                command.Parameters[@"client_id"].Value = this.client_id;
                command.Parameters[@"product_id"].Value = product.Key;
                command.Parameters[@"date"].Value = date;
                command.Parameters["@product_count"].Value = product.Value;
                command.Parameters["@cost"].Value = cost;

                try
                {
                    command.ExecuteNonQuery();
                }

                catch (SqlException ex)
                {
                    Debug.WriteLine(ex.Message);
                }
            }

            connection.Close();
        }

        public int ClientID
        {
            get { return this.client_id; }
        }

        public Dictionary<int, int> Pruducts
        {
            get { return this.products; }
        }
    }
}
