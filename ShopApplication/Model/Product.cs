using System.Data.SqlClient;
using System.Diagnostics;

namespace ShopApplication.Model
{
    public class Product
    {
        private string name;
        private double cost;
        private string type;

        public Product(string name, double cost, string type)
        {
            this.name = name;
            this.cost = cost;
            this.type = type;
        }

        public static void ClearProductTable(SqlConnection connection)
        {
            Purchase.ClearPurchaseTable(connection);

            connection.Open();

            SqlCommand command = connection.CreateCommand();

            command.CommandText = @"DELETE FROM Shop.dbo.Products";
            command.ExecuteNonQuery();

            command.CommandText = @"DBCC CHECKIDENT ('Shop.dbo.Products', RESEED, 0)";
            command.ExecuteNonQuery();

            connection.Close();
        }

        public void AddToDatabase(SqlConnection connection)
        {
            connection.Open();

            SqlCommand command = connection.CreateCommand();

            command.CommandText = @"INSERT INTO Shop.dbo.Products (Name, Cost, Type) VALUES (@name, @cost, @type)";
            command.Parameters.AddWithValue(@"name", this.name);
            command.Parameters.AddWithValue(@"cost", this.cost);
            command.Parameters.AddWithValue(@"type", this.type);

            try
            {
                command.ExecuteNonQuery();
            }

            catch(SqlException ex)
            {
                Debug.WriteLine(ex.Message);
            }

            connection.Close();
        }

        public string Name
        {
            get { return this.name; }
        }

        public double Cost
        {
            get { return this.cost; }
        }

        public string Type
        {
            get { return this.type; }
        }
    }
}
