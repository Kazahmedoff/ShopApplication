using Microsoft.ProjectOxford.Face.Contract;
using System.Data.SqlClient;
using System.Diagnostics;

namespace ShopApplication.Model
{
    public class Client
    {
        private Face face;
        private string photo_url;
        private int id;

        public Client(Face face, string photo_url)
        {
            this.photo_url = photo_url;
            this.face = face;
        }

        public static void ClearClientTable(SqlConnection connection)
        {
            Purchase.ClearPurchaseTable(connection);

            connection.Open();

            SqlCommand command = connection.CreateCommand();

            command.CommandText = @"DELETE FROM Shop.dbo.Clients";
            command.ExecuteNonQuery();

            command.CommandText = @"DBCC CHECKIDENT ('Shop.dbo.Clients', RESEED, 0)";
            command.ExecuteNonQuery();

            connection.Close();
        }

        public void AddToDatabase(SqlConnection connection)
        {
            connection.Open();

            SqlCommand command = connection.CreateCommand();

            command.CommandText = @"INSERT INTO Shop.dbo.Clients (Age, Gender, PhotoURL) VALUES (@age, @gender, @photo_url)";
            command.Parameters.AddWithValue(@"age", this.face.FaceAttributes.Age);
            command.Parameters.AddWithValue(@"gender", this.face.FaceAttributes.Gender);
            command.Parameters.AddWithValue(@"photo_url", this.photo_url);

            try
            {
                command.ExecuteNonQuery();
            }

            catch(SqlException ex)
            {
                Debug.WriteLine(ex.Message);
            }

            command.CommandText = string.Format("SELECT Client_ID FROM Shop.dbo.Clients WHERE PhotoURL = '{0}'", 
                this.photo_url);

            using (SqlDataReader reader = command.ExecuteReader())
            {
                if (reader.HasRows)
                {
                    reader.Read();
                    this.id = reader.GetInt32(reader.GetOrdinal("Client_ID"));
                }
            }

            connection.Close();
        }

        public string Gender
        {
            get { return this.face.FaceAttributes.Gender; }
        }

        public double Age
        {
            get { return this.face.FaceAttributes.Age; }
        }

        public string PhotoURL
        {
            get { return this.photo_url; }
        }

        public int ID
        {
            get { return this.id; }
        }
    }
}
