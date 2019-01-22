using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.ProjectOxford.Face;
using Microsoft.ProjectOxford.Face.Contract;
using ShopApplication.Model;
using System.Data.SqlClient;
using System.Linq;

namespace ShopApplication
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly IFaceServiceClient face_service_client = 
            new FaceServiceClient("3e13faf1b18446719fc0107701382760", "https://westcentralus.api.cognitive.microsoft.com/face/v1.0");

        SqlConnection connection;
        Purchase purchase;
        Dictionary<int, int> products;
        Client client;

        Face[] client_faces;
        string[] face_descriptions;
        double resize_factor;

        public MainWindow()
        {
            InitializeComponent();

            this.connection = new SqlConnection(@"Data Source=KIRILL-PC;Integrated Security=True;Connect Timeout=30;Encrypt=False;
                                                    TrustServerCertificate=True;ApplicationIntent=ReadWrite;MultiSubnetFailover=False");
            this.products = new Dictionary<int, int>();
        }

        //Load image
        private async void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            // Get the image file to scan from the user.
            var openDlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JPEG Image(*.jpg)|*.jpg"
            };
            bool? result = openDlg.ShowDialog(this);

            // Return if canceled.
            if (!(bool)result)
            {
                return;
            }

            // Display the image file.
            string filePath = openDlg.FileName;

            this.DefineClientGroup(filePath);

            Uri fileUri = new Uri(filePath);
            BitmapImage bitmapSource = new BitmapImage();

            bitmapSource.BeginInit();
            bitmapSource.CacheOption = BitmapCacheOption.None;
            bitmapSource.UriSource = fileUri;
            bitmapSource.EndInit();

            FacePhoto.Source = bitmapSource;

            // Detect any faces in the image.
            Title = "Detecting...";
            client_faces = await UploadAndDetectFaces(filePath);
            Title = String.Format("Detection Finished. {0} face(s) detected", client_faces.Length);

            if (client_faces.Length > 0)
            {
                // Prepare to draw rectangles around the faces.
                DrawingVisual visual = new DrawingVisual();
                DrawingContext drawingContext = visual.RenderOpen();
                drawingContext.DrawImage(bitmapSource,
                    new Rect(0, 0, bitmapSource.Width, bitmapSource.Height));

                double dpi = bitmapSource.DpiX;
                resize_factor = 96 / dpi;
                face_descriptions = new string[client_faces.Length];

                for (int i = 0; i < client_faces.Length; ++i)
                {
                    Face face = client_faces[i];

                    // Draw a rectangle on the face.
                    drawingContext.DrawRectangle(
                        Brushes.Transparent,
                        new Pen(Brushes.Red, 2),
                        new Rect(
                            face.FaceRectangle.Left * resize_factor,
                            face.FaceRectangle.Top * resize_factor,
                            face.FaceRectangle.Width * resize_factor,
                            face.FaceRectangle.Height * resize_factor
                            )
                    );

                    // Store the face description.
                    face_descriptions[i] = FaceDescription(face);
                }

                drawingContext.Close();

                // Display the image with the rectangle around the face.
                RenderTargetBitmap faceWithRectBitmap = new RenderTargetBitmap(
                    (int)(bitmapSource.PixelWidth * resize_factor),
                    (int)(bitmapSource.PixelHeight * resize_factor),
                    96,
                    96,
                    PixelFormats.Pbgra32);

                faceWithRectBitmap.Render(visual);
                FacePhoto.Source = faceWithRectBitmap;

                // Set the status bar text.
                FaceDescriptionBox.Text = "Place the mouse pointer over a face to see the face description.";

                client = new Client(client_faces[0], filePath);
                client.AddToDatabase(connection);
            }
        }

        // Displays the face description when the mouse is over a face rectangle.

        private void FacePhoto_MouseMove(object sender, MouseEventArgs e)
        {
            // If the REST call has not completed, return from this method.
            if (client_faces == null)
                return;

            // Find the mouse position relative to the image.
            Point mouseXY = e.GetPosition(FacePhoto);

            ImageSource imageSource = FacePhoto.Source;
            BitmapSource bitmapSource = (BitmapSource)imageSource;

            // Scale adjustment between the actual size and displayed size.
            var scale = FacePhoto.ActualWidth / (bitmapSource.PixelWidth / resize_factor);

            // Check if this mouse position is over a face rectangle.
            bool mouseOverFace = false;

            for (int i = 0; i < client_faces.Length; ++i)
            {
                FaceRectangle fr = client_faces[i].FaceRectangle;
                double left = fr.Left * scale;
                double top = fr.Top * scale;
                double width = fr.Width * scale;
                double height = fr.Height * scale;

                // Display the face description for this face if the mouse is over this face rectangle.
                if (mouseXY.X >= left && mouseXY.X <= left + width && mouseXY.Y >= top && mouseXY.Y <= top + height)
                {
                    FaceDescriptionBox.Text = "Client ID = " + this.client.ID.ToString() + ", " + face_descriptions[i];
                    mouseOverFace = true;
                    break;
                }
            }

            // If the mouse is not over a face rectangle.
            if (!mouseOverFace)
                FaceDescriptionBox.Text = "Place the mouse pointer over a face to see the face description.";
        }

        // Uploads the image file and calls Detect Faces.
        private async Task<Face[]> UploadAndDetectFaces(string imageFilePath)
        {
            // The list of Face attributes to return.
            IEnumerable<FaceAttributeType> faceAttributes =
                new FaceAttributeType[] { FaceAttributeType.Gender, FaceAttributeType.Age, FaceAttributeType.Smile, FaceAttributeType.Emotion, FaceAttributeType.Glasses, FaceAttributeType.Hair };

            // Call the Face API.
            try
            {
                using (Stream imageFileStream = File.OpenRead(imageFilePath))
                {
                    Face[] faces = await face_service_client.DetectAsync(imageFileStream, returnFaceId: true, returnFaceLandmarks: false, returnFaceAttributes: faceAttributes);
                    return faces;
                }
            }
            // Catch and display Face API errors.
            catch (FaceAPIException f)
            {
                MessageBox.Show(f.ErrorMessage, f.ErrorCode);
                return new Face[0];
            }
            // Catch and display all other errors.
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "Error");
                return new Face[0];
            }
        }

        // Returns a string that describes the given face.

        private string FaceDescription(Face face)
        {
            StringBuilder sb = new StringBuilder();

            sb.Append("Face: ");

            // Add the gender, age, and smile.
            sb.Append(face.FaceAttributes.Gender);
            sb.Append(", ");
            sb.Append(face.FaceAttributes.Age);
            sb.Append(", ");
            sb.Append(String.Format("smile {0:F1}%, ", face.FaceAttributes.Smile * 100));

            // Add glasses.
            sb.Append(face.FaceAttributes.Glasses);

            // Return the built string.
            return sb.ToString();
        }

        private void clearClientTableButton_Click(object sender, RoutedEventArgs e)
        {
            Client.ClearClientTable(this.connection);
        }

        private void clearProductTableButton_Click(object sender, RoutedEventArgs e)
        {
            Product.ClearProductTable(this.connection);
        }

        private void clearPurchaseTableButton_Click(object sender, RoutedEventArgs e)
        {
            Purchase.ClearPurchaseTable(this.connection);
        }

        private void clearDatabaseButton_Click(object sender, RoutedEventArgs e)
        {
            Purchase.ClearPurchaseTable(this.connection);
            Client.ClearClientTable(this.connection);
            Product.ClearProductTable(this.connection);
        }

        //Adding new product to DataBase
        private void addProductButton_Click(object sender, RoutedEventArgs e)
        {
            Product product;
            string name;
            double cost;
            string type;

            if (this.productNameBox.Text != string.Empty && this.productCostBox.Text != string.Empty &&
                this.productTypeBox.Text != string.Empty)
            {
                name = this.productNameBox.Text;
                type = this.productTypeBox.Text;

                try
                {
                    cost = Convert.ToDouble(this.productCostBox.Text);
                }

                catch (FormatException)
                {
                    MessageBox.Show("It is not number!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            else
            {
                MessageBox.Show("Enter the product data!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            product = new Product(name, cost, type);
            product.AddToDatabase(this.connection);

            this.productNameBox.Clear();
            this.productCostBox.Clear();
            this.productTypeBox.Clear();
        }

        //Add product to client product list
        private void addToProductListButton_Click(object sender, RoutedEventArgs e)
        {
            int id, count;

            if(this.idProductBox.Text != string.Empty && this.countProductBox.Text != string.Empty)
            {
                try
                {
                    id = Convert.ToInt32(this.idProductBox.Text);
                    count = Convert.ToInt32(this.countProductBox.Text);
                }

                catch(FormatException)
                {
                    MessageBox.Show("It is not integer!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            else
            {
                MessageBox.Show("Enter the data!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            this.products.Add(id, count);

            this.idProductBox.Clear();
            this.countProductBox.Clear();
        }

        //Reset client product list
        private void cancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.products.Clear();

            this.idProductBox.Clear();
            this.countProductBox.Clear();
        }

        //Add purchase data too database
        private void payButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.products.Count != 0 && client_faces.Length != 0)
            {
                this.purchase = new Purchase(this.client.ID, products);
                this.purchase.AddToDatabase(this.connection);
                this.products.Clear();
            }

            else
                MessageBox.Show("Product list is empty!", "Message", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        //Compare client faces
        private async void DefineClientGroup(string filePath)
        {
            // Create an empty person group
            string personGroupId = "clients";
            try
            {
                await face_service_client.CreatePersonGroupAsync(personGroupId, "Clients");
            }

            catch(Exception) { }

            // Define Andr
            CreatePersonResult client2 = await face_service_client.CreatePersonAsync(personGroupId, "Mashulya");

            // Directory contains image files of Andr
            const string client2ImageDir = @"C:\Users\Kirill\Desktop\Mashulya\";

            foreach (string imagePath in Directory.GetFiles(client2ImageDir, "*.jpg"))
            {
                using (Stream s = File.OpenRead(imagePath))
                {
                    // Detect faces in the image and add to Andr
                    await face_service_client.AddPersonFaceAsync(
                        personGroupId, client2.PersonId, s);
                }
            }

            await face_service_client.TrainPersonGroupAsync(personGroupId);

            TrainingStatus trainingStatus = null;
            while (true)
            {
                trainingStatus = await face_service_client.GetPersonGroupTrainingStatusAsync(personGroupId);

                if (trainingStatus.Status != Status.Running)
                {
                    break;
                }

                await Task.Delay(1000);
            }

            using (Stream s = File.OpenRead(filePath))
            {
                var faces = await face_service_client.DetectAsync(s);
                var faceIds = faces.Select(face => face.FaceId).ToArray();

                var results = await face_service_client.IdentifyAsync(personGroupId, faceIds);
                foreach (var identifyResult in results)
                {
                    Console.WriteLine("Result of face: {0}", identifyResult.FaceId);
                    if (identifyResult.Candidates.Length == 0)
                    {
                        MessageBox.Show("No one identified");
                    }
                    else
                    {
                        // Get top 1 among all candidates returned
                        var candidateId = identifyResult.Candidates[0].PersonId;
                        var person = await face_service_client.GetPersonAsync(personGroupId, candidateId);
                        MessageBox.Show("Identified as " + person.Name);
                    }
                }
            }
        }
    }
}
