using MySql.Data.MySqlClient; //MySQL data package
using System;
using System.Collections.Generic; //data structures like lists and dictionaries
using System.Data;
using System.Drawing.Drawing2D; //to redraw images for resizing
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using static _07_Toolset.Toolset.SQLInterface;
using static System.Net.Mime.MediaTypeNames;

namespace _07_Toolset
{
    public class Toolset
    {
        //V1.1
        //Removed test account
        //fixed ambiguous System.Drawing.Image
        //added GetAllFromTier to SQLInterface
        //added DeleteEntry to SQLInterface

        //v2.6 Discovered yet another way how not to commit changes

        // All the variables
        //
        //Configuration files
        public string MySQLConfigLocation = "C:\\Develop\\MySQL.txt"; //Location of the configuration files
        public string MyWebhostConfigLocation = "C:\\Develop\\Webhost.txt"; //Location of the configuration files

        //Connection string to the local MySQL database
        //TODO test a few versions
        public string MySQLConnectionString = "";

        //Connection info for webhost
        public string DatabaseHost = "";
        public string DatabaseName = "";
        public string DatabaseUsername = "";
        public string DatabasePassword = "";


        //
        //Local files
        //Interactions with local files

        //Returns the selected file path with extension
        public string GetSelectedFilePath(string filter = "All Files (*.*)|*.*")
        {
            //Create a new instance of the OpenFileDialog
            OpenFileDialog openFileDialog = new OpenFileDialog();

            //Set the file filter and dialog title
            openFileDialog.Filter = filter;
            openFileDialog.Title = "Select a File";

            //Show the dialog and check if the user clicked OK
            DialogResult result = openFileDialog.ShowDialog();

            if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(openFileDialog.FileName))
            {
                //Return the selected file path (full path + extension)
                return openFileDialog.FileName;
            }
            else
            {
                //Return an empty string or null if the user cancelled
                return string.Empty;
            }
        }

        //Loads the MySQL configuration and sets the class variables
        public async void LoadMySQLConfigAsync()
        {
            if (File.Exists(MySQLConfigLocation))
            {
                try
                {
                    string fileContent = File.ReadAllText(MySQLConfigLocation);

                    //Use regex to get the values from the text file
                    var Server = GetValue(fileContent, "server");
                    var database = GetValue(fileContent, "database");
                    var uid = GetValue(fileContent, "uid");
                    var pwd = GetValue(fileContent, "pwd");
                    MySQLConnectionString = $"server={Server};database={database};uid={uid};pwd={pwd};";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error reading MySQL configuration from file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                MessageBox.Show($"MySQL configuration file not found at {MySQLConfigLocation}.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

            }
        }

        

        //Used to get values from a textfile
        public string GetValue(string content, string key)
        {
            // The regex now captures the value until the end of the line
            var match = Regex.Match(content, $@"{key}\s*=\s*(.*)");
            if (match.Success)
            {
                // Trim leading/trailing whitespace
                string value = match.Groups[1].Value.Trim();
                return value;
            }
            return string.Empty;
        }

        //Takes an image and returns a redrawn (resized) image
        public static System.Drawing.Image ResizeImage(System.Drawing.Image image, int newWidth, int newHeight)
        {
            // Create a new bitmap with the specified dimensions
            var newImage = new Bitmap(newWidth, newHeight);

            // Get the original dimensions
            var originalWidth = image.Width;
            var originalHeight = image.Height;

            // Calculate the aspect ratio
            float ratioX = (float)newWidth / originalWidth;
            float ratioY = (float)newHeight / originalHeight;
            float ratio = Math.Min(ratioX, ratioY);

            // Calculate the new size while maintaining the aspect ratio
            int destWidth = (int)(originalWidth * ratio);
            int destHeight = (int)(originalHeight * ratio);

            using (var graphics = Graphics.FromImage(newImage))
            {
                // Set quality settings
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;

                // Draw the original image onto the new bitmap
                graphics.DrawImage(image, new Rectangle(0, 0, destWidth, destHeight));
            }

            return newImage;
        }


        //
        //MySQL
        //Contains all the SQL related functions
        //requires MySql.Data package (NuGet)
        //TODO currently built for my own use, can be made dynamic for generic use
        public class SQLInterface
        {
            string ConnectionString = "";
            //Note that when exiting a using block, the Dispose() method is called on the object automatically. It returns the physical connection to a ready-to-use pool and reuses it

            public SQLInterface(string mySQLConnectionString)
            {
                ConnectionString = mySQLConnectionString;
            }

            // Counts the number of entries in the imageentries table where the 'columnName' field contains the word 'contains'.
            public int CountQueuedEntries(string columnName, string contains)
            {
                // The LIKE operator with '%' wildcards makes sure it returns any entry containing 'contains'.
                string query = "SELECT COUNT(*) FROM imageentries WHERE " + columnName + " LIKE '%" + contains + "%';";

                int count = -1; // Default error value

                //use the connection
                using (MySqlConnection connection = new MySqlConnection(ConnectionString))
                {
                    //to use the command
                    using (MySqlCommand command = new MySqlCommand(query, connection))
                    {
                        try
                        {
                            connection.Open();

                            // ExecuteScalar is used for queries that return a single value
                            object result = command.ExecuteScalar();

                            if (result != null)
                            {
                                //ExecuteScalar() gave us an object, convert it to an integer
                                count = Convert.ToInt32(result);
                            }
                        }
                        catch (MySqlException ex)
                        {
                            // Handle MySQL-specific errors
                            MessageBox.Show($"MySQL Error: {ex.Message}", "Database Query Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                        catch (Exception ex)
                        {
                            // Handle general exceptions
                            MessageBox.Show($"An unexpected error occurred: {ex.Message}", "Application Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                }
                return count;
            }

            //returns a list of all unique tier levels
            public List<String> GetTiers()
            {
                //prepare return type
                List<String> entries = new List<String>();

                //prepare query
                string sqlQuery = "SELECT DISTINCT tier FROM imageentries;";

                // Use the MySqlConnection for MySQL
                using (MySqlConnection connection = new MySqlConnection(ConnectionString))
                {
                    using (MySqlCommand command = new MySqlCommand(sqlQuery, connection))
                    {
                        try
                        {
                            connection.Open();
                            // ExecuteReader is used for SELECT statements
                            using (MySqlDataReader reader = command.ExecuteReader())
                            {
                                //Loop to process ALL rows returned by the reader.
                                while (reader.Read())
                                {
                                    // Add the unique name to the list
                                    entries.Add(reader.GetString("Tier"));
                                }
                            }
                        }
                        catch (MySqlException ex)
                        {
                            // Handle MySQL-specific errors
                            MessageBox.Show($"MySQL Error during data retrieval: {ex.Message}", "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                        catch (Exception ex)
                        {
                            // Handle other unexpected errors
                            MessageBox.Show($"An unexpected error occurred during retrieval: {ex.Message}", "Application Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                }
                return entries;
            }

            //returns a list of tickets from a specific tier
            public List<TicketParameters> GetAllFromTier(string tier)
            {

                //prepare return type
                List<TicketParameters> entries = new List<TicketParameters>();

                string sqlQuery = "SELECT * FROM `imageentries` WHERE tier LIKE '%" + tier + "%' AND Status NOT LIKE '%queued%' ORDER BY ImageID ASC;";

                // Use the MySqlConnection for MySQL
                using (MySqlConnection connection = new MySqlConnection(ConnectionString))
                {
                    using (MySqlCommand command = new MySqlCommand(sqlQuery, connection))
                    {
                        try
                        {
                            connection.Open();
                            // ExecuteReader is used for SELECT statements
                            using (MySqlDataReader reader = command.ExecuteReader())
                            {
                                // 3. Change the loop to process ALL rows returned by the reader.
                                while (reader.Read())
                                {
                                    // Create a new TicketParameters object for EACH row
                                    TicketParameters parameters = new TicketParameters
                                    {
                                        // --- String Fields ---
                                        Prompt = reader.GetString("Prompt"),
                                        NegativePrompt = reader.GetString("NegativePrompt"),
                                        SamplerName = reader.GetString("SamplerName"),
                                        HiresUpscaler = reader.GetString("Hiresupscaler"),
                                        Tier = reader.GetString("Tier"),

                                        // --- Integer Fields ---
                                        Steps = reader.GetInt32("Steps"),
                                        Width = reader.GetInt32("Width"),
                                        Height = reader.GetInt32("Height"),
                                        BatchSize = reader.GetInt32("BatchSize"),
                                        HiresSteps = reader.GetInt32("HiresSteps"),
                                        ImageID = reader.GetInt32("ImageID"),

                                        // --- Float Fields ---
                                        CfgScale = reader.GetFloat("CfgScale"),
                                        HiresDenoisingStrength = reader.GetFloat("HiresDenoisingStrength"),
                                        HiresUpscaleBy = reader.GetFloat("HiresUpscaleBy"),

                                        // --- Boolean Fields ---
                                        HiresFix = reader.GetBoolean("HiresFix"),

                                        // --- Nullable/Optional Fields ---
                                        // Check for DBNull before reading the value for nullable types
                                        Seed = reader.IsDBNull(reader.GetOrdinal("Seed")) ? (long?)null : reader.GetInt64("Seed"),
                                        Status = reader.IsDBNull(reader.GetOrdinal("Status")) ? null : reader.GetString("Status"),
                                        Location = reader.IsDBNull(reader.GetOrdinal("Location")) ? null : reader.GetString("Location"),
                                        Origin = reader.IsDBNull(reader.GetOrdinal("Origin")) ? null : reader.GetString("Origin"),
                                        //Safe = reader.IsDBNull(reader.GetOrdinal("Safe")) ? (bool?)null : reader.GetBoolean("Safe"),
                                    };

                                    // Add the newly mapped object to the list
                                    entries.Add(parameters);
                                }
                            }
                        }
                        catch (MySqlException ex)
                        {
                            // Handle MySQL-specific errors
                            MessageBox.Show($"MySQL Error during data retrieval: {ex.Message}", "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                        catch (Exception ex)
                        {
                            // Handle other unexpected errors
                            MessageBox.Show($"An unexpected error occurred during retrieval: {ex.Message}", "Application Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                }

                return entries;
            }

            //provides entry insertion capabilities
            public class MySQLDataInserter
            {
                string ConnectionString = "";

                public MySQLDataInserter(string mySQLConnectionString)
                {
                    ConnectionString = mySQLConnectionString;
                }

                // SQL statement with placeholders (@ColumnName) for all properties in class.
                private const string InsertQuery = @"
            INSERT INTO imageentries (
            Prompt, NegativePrompt, Steps, Width, Height, Seed, CfgScale, 
            SamplerName, BatchSize, HiresFix, HiresUpscaler, HiresSteps, 
            HiresDenoisingStrength, HiresUpscaleBy, imageID, status, tier, safe,
            Location, Origin
        ) 
        VALUES (
            @Prompt, @NegativePrompt, @Steps, @Width, @Height, @Seed, @CfgScale, 
            @SamplerName, @BatchSize, @HiresFix, @HiresUpscaler, @HiresSteps, 
            @HiresDenoisingStrength, @HiresUpscaleBy, @imageID, @status, @tier, @safe,
            @Location, @Origin
        );";

                // Inserts a new row into the imageentries table using the provided parameters. Returns success or failure
                public bool InsertEntry(TicketParameters parameters)
                {
                    using (MySqlConnection connection = new MySqlConnection(ConnectionString))
                    {
                        using (MySqlCommand command = new MySqlCommand(InsertQuery, connection))
                        {
                            //Add parameters to the command object
                            AddParameters(command, parameters);

                            try
                            {
                                connection.Open();
                                //Execute the command; ExecuteNonQuery returns the number of rows affected.
                                int rowsAffected = command.ExecuteNonQuery();

                                return rowsAffected > 0;
                            }
                            catch (MySqlException ex)
                            {
                                MessageBox.Show($"MySQL Error during insert: {ex.Message}", "Database Insert Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                return false;
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show($"An unexpected error occurred during insert: {ex.Message}", "Application Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                return false;
                            }
                        }
                    }
                }

                /// Helper method to map C# properties to SQL command parameters.
                private void AddParameters(MySqlCommand command, TicketParameters p)
                {
                    command.Parameters.AddWithValue("@Prompt", p.Prompt);
                    command.Parameters.AddWithValue("@NegativePrompt", p.NegativePrompt);
                    command.Parameters.AddWithValue("@Steps", p.Steps);
                    command.Parameters.AddWithValue("@Width", p.Width);
                    command.Parameters.AddWithValue("@Height", p.Height);

                    // Handle nullable long (Seed)
                    command.Parameters.AddWithValue("@Seed", p.Seed.HasValue ? (object)p.Seed.Value : DBNull.Value);

                    command.Parameters.AddWithValue("@CfgScale", p.CfgScale);
                    command.Parameters.AddWithValue("@SamplerName", p.SamplerName);
                    command.Parameters.AddWithValue("@BatchSize", p.BatchSize);
                    command.Parameters.AddWithValue("@HiresFix", p.HiresFix);
                    command.Parameters.AddWithValue("@HiresUpscaler", p.HiresUpscaler);
                    command.Parameters.AddWithValue("@HiresSteps", p.HiresSteps);
                    command.Parameters.AddWithValue("@HiresDenoisingStrength", p.HiresDenoisingStrength);
                    command.Parameters.AddWithValue("@HiresUpscaleBy", p.HiresUpscaleBy);
                    command.Parameters.AddWithValue("@imageID", p.ImageID);

                    // Handle nullable strings (status, tier, safe)
                    command.Parameters.AddWithValue("@status", string.IsNullOrEmpty(p.Status) ? (object)DBNull.Value : p.Status);
                    command.Parameters.AddWithValue("@tier", string.IsNullOrEmpty(p.Tier) ? (object)DBNull.Value : p.Tier);
                    command.Parameters.AddWithValue("@safe", p.Safe);
                    command.Parameters.AddWithValue("@Origin", string.IsNullOrEmpty(p.Origin) ? (object)DBNull.Value : p.Origin);
                    command.Parameters.AddWithValue("@Location", string.IsNullOrEmpty(p.Location) ? (object)DBNull.Value : p.Location);
                }
            }

            //provides update capabilities
            public class MySQLDataUpdater
            {
                string ConnectionString = "";
                public MySQLDataUpdater(string mySQLConnectionString)
                {
                    ConnectionString = mySQLConnectionString;
                }

                public bool UpdateStatusAndTier(TicketParameters row, string ConnectionString)
                {
                    //Set the new values WHERE imageID (unique key) matches.
                    string updateQuery = @"
            UPDATE imageentries 
            SET status = @NewStatus, tier = @NewTier
            WHERE imageID = @ImageID;";

                    using (MySqlConnection connection = new MySqlConnection(ConnectionString))
                    {
                        using (MySqlCommand command = new MySqlCommand(updateQuery, connection))
                        {
                            //Add Parameters. Using AddWithValue prevents SQL injection
                            command.Parameters.AddWithValue("@NewStatus", row.Status);
                            command.Parameters.AddWithValue("@NewTier", row.Tier);
                            command.Parameters.AddWithValue("@ImageID", row.ImageID);

                            try
                            {
                                connection.Open();

                                //ExecuteNonQuery returns the number of rows affected.
                                int rowsAffected = command.ExecuteNonQuery();

                                //Returns true only if exactly one row was updated.
                                if (rowsAffected == 1)
                                {
                                    return true;
                                }
                                else if (rowsAffected == 0)
                                {
                                    MessageBox.Show($"Update complete, but no row was found with imageID: {row.ImageID}.", "Update Status", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                    return false;
                                }
                                else
                                {
                                    // This indicates a severe problem since imageID should be unique.
                                    MessageBox.Show($"Critical Error: Multiple rows ({rowsAffected}) were updated for imageID: {row.ImageID}.", "Update Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                    return false;
                                }
                            }
                            catch (MySqlException ex)
                            {
                                MessageBox.Show($"MySQL Error during update: {ex.Message}", "Database Update Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                return false;
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show($"An unexpected error occurred during update: {ex.Message}", "Application Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                return false;
                            }
                        }
                    }
                }
            }

            public bool DeleteEntry(string imageID)
            {
                string DeleteQuery = @"
        DELETE FROM imageentries
        WHERE imageID = @imageID;";

                using (MySqlConnection connection = new MySqlConnection(ConnectionString))
                {
                    using (MySqlCommand command = new MySqlCommand(DeleteQuery, connection))
                    {
                        // Add the imageID parameter to the command
                        command.Parameters.AddWithValue("@imageID", imageID);

                        try
                        {
                            connection.Open();
                            // ExecuteNonQuery returns the number of rows affected (deleted)
                            int rowsAffected = command.ExecuteNonQuery();

                            return rowsAffected > 0;
                        }
                        catch (MySqlException ex)
                        {
                            MessageBox.Show($"MySQL Error during delete: {ex.Message}", "Database Delete Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return false;
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"An unexpected error occurred during delete: {ex.Message}", "Application Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return false;
                        }
                    }
                }
            }


            //My database uses tickets.
            //TODO not sure whether a static class can be designed that will handle universal variables to insert. Worth figuring out.
            public class TicketParameters
            {
                //These are all the thigns i'll need to generate the image
                public string Prompt { get; set; } //Holy prompt
                public string NegativePrompt { get; set; } // Values we don't want
                public int Steps { get; set; }
                public int Width { get; set; }
                public int Height { get; set; }
                public long? Seed { get; set; }
                public float CfgScale { get; set; } // lower is freeer, higher (7-12) is closer to prompt
                public string SamplerName { get; set; } // Generation sampling method (Euler, DPM++ 2M, DPM Fast etc 19 available) 
                public int BatchSize { get; set; } // Create multiple results, seed is increased by 1 each itteration
                                                   //public string Vae { get; set; } // Not yet
                public bool HiresFix { get; set; }
                public string HiresUpscaler { get; set; }
                public int HiresSteps { get; set; }
                public float HiresDenoisingStrength { get; set; }
                public float HiresUpscaleBy { get; set; }
                public int ImageID { get; set; }
                public string Status { get; set; }
                public string Tier { get; set; }
                public bool? Safe { get; set; }
                // public GeneratedImageResult result { get; set; }

                public string Location { get; set; }
                public string Origin { get; set; }

                public System.Drawing.Image Preview { get; set; }
            }
        }

        //
        //Webhosting
        //Interaction with a webhost via FTP or SFTP
        //
        public class WebhostInterface
        {
            public string WebhostConfigLocation = "";

            //Connection details for webhost
            public string DatabaseHost = "";
            public string DatabaseName = "";
            public string DatabaseUsername = "";
            public string DatabasePassword = "";

            //Required for FTP access:
            public string FtpHost = "";
            public string FtpUsername = "";
            public string FtpPassword = "";

            public WebhostInterface(string ConfigLocation)
            {
                //Load settings on instantiation
                LoadWebhostConfigAsync(ConfigLocation);
            }

            //Gets values from a textfile.
            public string GetValue(string content, string key)
            {
                // The regex now captures the value until the end of the line
                var match = Regex.Match(content, $@"{key}\s*=\s*(.*)");
                if (match.Success)
                {
                    // Trim leading/trailing whitespace
                    string value = match.Groups[1].Value.Trim();
                    return value;
                }
                return string.Empty;
            }

            //Loads the settings from a textfile. For my test environment that is fine but I expect a more secure source.
            public async void LoadWebhostConfigAsync(string WebhostConfigLocation)
            {
                if (File.Exists(WebhostConfigLocation))
                {
                    try
                    {
                        string fileContent = File.ReadAllText(WebhostConfigLocation);

                        DatabaseHost = GetValue(fileContent, "DatabaseHost");
                        DatabaseName = GetValue(fileContent, "DatabaseName");
                        DatabaseUsername = GetValue(fileContent, "DatabaseUsername");
                        DatabasePassword = GetValue(fileContent, "DatabasePassword");

                        FtpHost = GetValue(fileContent, "FtpHost");
                        FtpUsername = GetValue(fileContent, "FtpUsername");
                        FtpPassword = GetValue(fileContent, "FtpPassword");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error reading Webhost configuration from file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                else
                {
                    MessageBox.Show($"Webhost configuration file not found at {WebhostConfigLocation}.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

                }
            }

            //Shows a messagebox with the connection status. Good for testing.
            public void CheckConnection()
            {
                string connectionString =
                    $"Server={DatabaseHost};" +
                    $"Database={DatabaseName};" +
                    $"Uid={DatabaseUsername};" +
                    $"Pwd={DatabasePassword};";

                //Attempt the connection
                using (MySqlConnection connection = new MySqlConnection(connectionString))
                {
                    try
                    {
                        // Try to open the connection
                        connection.Open();

                        // Success
                        MessageBox.Show($"Connection Successful.", "Connection Success", MessageBoxButtons.OK, MessageBoxIcon.Error);

                        // Always close the connection after testing
                        connection.Close();
                    }
                    catch (MySqlException ex)
                    {
                        // Failure
                        // Display the specific error for debugging
                        MessageBox.Show($"Connection Error: {ex.Message}\n\nCheck Host, Name, User, and Password.", "Connection Failure", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }

            //Uploads an image (FTP) and their database entry to the webhost database and datastore.
            public void UploadToWebhost(TicketParameters newTicket)
            {
                //Prepare the data to be Uploaded
                int ImageID = newTicket.ImageID;
                string Prompt = newTicket.Prompt.ToString();

                //Local File Path (using the path you provided)
                string localFilePath = newTicket.Location;

                //Generate a unique, safe filename based on ID and timestamp. This assumes there won't be two updates on the same file in the same second. Seems fair.
                string originalFileName = Path.GetFileName(localFilePath);
                string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                string uniqueFilename = $"{ImageID}_{timestamp}_{originalFileName}";

                //Prepare the remote FTP URI and the public HTTP URL
                string remoteFtpPath = $"Stable/{uniqueFilename}"; // Remote folder structure
                string remoteFtpUri = $"ftp://{FtpHost}/{remoteFtpPath}";

                //The final public URL that will be saved to the database
                string finalFileLocation = $"https://maartenschous.nl/Stable/{uniqueFilename}";

                //File Upload via FTP
                bool uploadSuccess = UploadFileFtp(localFilePath, remoteFtpUri, FtpUsername, FtpPassword);

                if (!uploadSuccess)
                {
                    // If the FTP upload failed, we stop here and do not insert the broken link into the database.
                    return;
                }

                //Ensure the connection details are available
                string connectionString =
                    $"Server={DatabaseHost};" +
                    $"Database={DatabaseName};" +
                    $"Uid={DatabaseUsername};" +
                    $"Pwd={DatabasePassword};";

                // Define the INSERT Query for the webhost database
                //Using parameterized queries (@Title, @Description) to prevent SQL Injection attacks.
                string insertQuery = "INSERT INTO Images (ID, Prompt, Filelocation) VALUES (@ID, @Prompt, @Filelocation);";

                using (MySqlConnection connection = new MySqlConnection(connectionString))
                {
                    using (MySqlCommand command = new MySqlCommand(insertQuery, connection))
                    {
                        try
                        {
                            //Open the connection
                            connection.Open();

                            //Add the parameters safely to the command
                            command.Parameters.AddWithValue("@ID", ImageID);
                            command.Parameters.AddWithValue("@Prompt", Prompt);
                            command.Parameters.AddWithValue("@Filelocation", finalFileLocation);//location on the webserver used for easy gallery

                            // ExecuteNonQuery returns the number of rows affected (should be 1 for a successful INSERT)
                            int rowsAffected = command.ExecuteNonQuery();

                            if (rowsAffected > 0)
                            {
                                // Success feedback if any
                            }
                            else
                            {
                                // Failure to insert feedback (though no exception was thrown)
                                MessageBox.Show("Data upload failed: File uploaded but no rows were inserted into the database.", "Upload Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }
                        catch (MySqlException ex)
                        {
                            // Handle connection or query errors
                            MessageBox.Show($"Error uploading data to webhost: {ex.Message}", "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                        finally
                        {
                            // The 'using' statement for the connection handles closing, but explicit checks are fine.
                            if (connection.State == System.Data.ConnectionState.Open)
                            {
                                connection.Close();
                            }
                        }
                    }
                }
            }

            //Uploads a file via FTP to the specified remote URI
            //TODO arguments could be taken from class variables instead of method parameters
            private bool UploadFileFtp(string localFilePath, string remoteUri, string username, string password)
            {
                // Check if the local file exists
                if (!File.Exists(localFilePath))
                {
                    MessageBox.Show($"Local file not found: {localFilePath}", "File Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }

                //Try the upload
                try
                {
                    //Create the FTP Request
                    FtpWebRequest request = (FtpWebRequest)WebRequest.Create(remoteUri);
                    request.Method = WebRequestMethods.Ftp.UploadFile;
                    request.Credentials = new NetworkCredential(username, password);
                    request.UseBinary = true; // Essential for image files
                    request.KeepAlive = false;

                    //Read the local file data
                    using (Stream fileStream = File.OpenRead(localFilePath))
                    {
                        //Get the request stream and copy the file contents
                        using (Stream requestStream = request.GetRequestStream())
                        {
                            fileStream.CopyTo(requestStream);
                        }
                    }

                    //Get the FTP response (to confirm success)
                    using (FtpWebResponse response = (FtpWebResponse)request.GetResponse())
                    {
                        // Check for a success status code
                        if (response.StatusCode == FtpStatusCode.ClosingData ||
                            response.StatusCode == FtpStatusCode.FileActionOK)
                        {
                            return true;
                        }
                        else
                        {
                            // Upload failed with a specific FTP status code
                            MessageBox.Show($"FTP Upload Failed: {response.StatusDescription}", "FTP Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return false;
                        }
                    }
                }
                catch (WebException ex)
                {
                    // Handle specific network/FTP errors
                    string status = $"FTP Error: {((FtpWebResponse)ex.Response)?.StatusDescription ?? ex.Message}";
                    MessageBox.Show(status, "FTP Network Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }
                catch (Exception ex)
                {
                    // Handle other general exceptions
                    MessageBox.Show($"General Error: {ex.Message}", "FTP Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }
            }
        }




    }
}
