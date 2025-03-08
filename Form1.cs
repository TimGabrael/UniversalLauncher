using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using Newtonsoft.Json;
using System.Diagnostics;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using System.Threading;
using Google.Apis.Download;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Security.Policy;
using System.Security.Cryptography;



namespace UniversalLauncher
{
    public partial class Form1 : Form
    {
        private const string settingsFilePath = "launcher_assets/settings.json";
        private const string driveCredentialsFilePath = "launcher_assets/credentials.json";
        private const string hashFile = "/launcher_assets/hashes.json";
        private string subProcessPath = "";
        private string folderId = "";
        private string apiKey = "";
        private string comparePath = "";
        private List<Tuple<string, string>> available_files;
        private bool autoStart = false;
        private ProgressBar progressBar;
        private Label progressLabel;
        private Button startButton;
        private DriveService driveService;
        private Task initializationTask;

        public Form1()
        {
            InitializeComponent();
            string exeDir = Path.GetDirectoryName(Application.ExecutablePath);
            Directory.SetCurrentDirectory(exeDir);
            LoadSettings();
            this.DoubleBuffered = true;
            initializationTask = InitializeDriveService();
        }

        async private Task InitializeDriveService()
        {
            try
            {
                driveService = new DriveService(new BaseClientService.Initializer()
                {
                    ApiKey = apiKey,
                    ApplicationName = "UniversalLauncher",
                    
                });
                available_files = await ListAllFiles(driveService, folderId);
                await DownloadFileByPath(hashFile);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading settings: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.Exit(-1);
            }
        }
        async private Task DownloadFileByPath(string fullPath)
        {
            try
            {
                string savePath = Path.GetFullPath(fullPath.Substring(1));
                bool downloaded = false;
                foreach(var file in available_files)
                {
                    if(file.Item1 == fullPath)
                    {
                        var request = driveService.Files.Get(file.Item2);
                        var stream = new MemoryStream();
                        await request.DownloadAsync(stream);
                        {
                            System.IO.FileInfo filePathEstablisher = new System.IO.FileInfo(savePath);
                            filePathEstablisher.Directory.Create();
                        }
                        using (var fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write))
                        {
                            stream.Position = 0;
                            stream.CopyTo(fileStream);
                        }

                        Debug.WriteLine($"File downloaded successfully: {savePath}");
                        downloaded = true;
                        break;
                    }
                }
                if (!downloaded)
                {
                    MessageBox.Show($"File couldn't be downloaded: {fullPath}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Environment.Exit(-1);
                }
            }
            catch (Exception e)
            {
                MessageBox.Show($"Download failed: {e.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.Exit(-1);
            }
        }
        async private Task<List<Tuple<string, string>>> ListAllFiles(DriveService service, string curFolderId)
        {
            FilesResource.ListRequest listRequest = service.Files.List();
            //listRequest.Q = "trashed = false";
            listRequest.Q = $"'{curFolderId}' in parents and trashed = false";
            listRequest.Fields = "nextPageToken, files(id, name, parents, mimeType)";
            listRequest.PageSize = 1000;
            List<Tuple<string, string, string, bool>> drive_content = new List<Tuple<string, string, string, bool>>();

            string pageToken = null;
            do
            {
                listRequest.PageToken = pageToken;
                var fileList = await listRequest.ExecuteAsync();
                if (fileList.Files != null && fileList.Files.Count > 0)
                {
                    foreach (var file in fileList.Files)
                    {
                        bool is_folder = file.MimeType == "application/vnd.google-apps.folder";
                        if (file.Parents != null && file.Parents.Count > 0)
                        {
                            drive_content.Add(new Tuple<string, string, string, bool>(file.Name, file.Id, file.Parents[0], is_folder));
                        }
                        else
                        {
                            drive_content.Add(new Tuple<string, string, string, bool>(file.Name, file.Id, "", is_folder));
                        }
                    }
                }
                pageToken = fileList.NextPageToken;
            } while (pageToken != null);

            List<Tuple<string, string>> parsed_content = new List<Tuple<string, string>>();
            foreach(var element in drive_content)
            {
                if (element.Item4)
                {
                    // is_folder
                    List<Tuple<string, string>> subList = await ListAllFiles(service, element.Item2);
                    foreach(var sub in subList)
                    {
                        string fullPath = element.Item1 + sub.Item1;

                        if (fullPath.Contains(comparePath + "/"))
                        {
                            fullPath = fullPath.Substring(fullPath.IndexOf(comparePath + "/"));
                        }
                        else if (fullPath.Contains("launcher_assets/hashes.json"))
                        {
                            fullPath = fullPath.Substring(fullPath.IndexOf("launcher_assets/hashes.json"));
                        }
                        parsed_content.Add(new Tuple<string, string>("/" + fullPath, sub.Item2));
                    }
                    continue;
                }
                parsed_content.Add(new Tuple<string, string>("/" + element.Item1, element.Item2));
            }
            return parsed_content;
        }


        public void LoadSettings()
        {
            if(File.Exists(settingsFilePath))
            {
                try
                {
                    string json = File.ReadAllText(settingsFilePath);
                    dynamic settings = JsonConvert.DeserializeObject(json);
                    string imagePath = settings.BackgroundImagePath;
                    subProcessPath = settings.SubProcess;
                    comparePath = settings.ComparePath;
                    autoStart = settings.AutoStart;
                    apiKey = settings.ApiKey;
                    folderId = settings.FolderId;
                    if(File.Exists(imagePath)) 
                    {
                        this.BackgroundImage = new System.Drawing.Bitmap(imagePath);
                        this.BackgroundImageLayout = ImageLayout.Stretch;
                    }
                    else
                    {
                        MessageBox.Show("Background Image not found", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                catch(Exception ex)
                {
                    MessageBox.Show($"Error loading settings: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                MessageBox.Show("Settings file not found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private void CenterForm()
        {
            Rectangle screenBounds = Screen.PrimaryScreen.Bounds;
            Console.WriteLine($"{screenBounds}");
            int formWidth = screenBounds.Width / 2;
            int formHeight = screenBounds.Height / 2;
            this.Size = new Size(formWidth, formHeight);

            int formX = screenBounds.Width / 2 - formWidth / 2;
            int formY = screenBounds.Height / 2 - formHeight / 2;
            this.Location = new Point(formX, formY);

            this.FormBorderStyle = FormBorderStyle.None;
        }

        private void AddProgressBar()
        {
            progressBar = new ProgressBar
            {
                Size = new Size(this.Width - 20, 20),
                Location = new Point(10, this.Height - 30),
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Style = ProgressBarStyle.Continuous,
            };
            this.Controls.Add(progressBar);
        }
        private void AddProgressLabel()
        {
            progressLabel = new Label
            {
                Size = new Size(progressBar.Width, progressBar.Height),
                Location = new Point(progressBar.Left, progressBar.Top - progressBar.Height - 20),
                Text = "",
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent,
                Font = new Font("Consolas", 20, FontStyle.Regular),
                AutoSize = true,
                ForeColor = Color.White,
            };
            this.Controls.Add(progressLabel);
        }
        private void AddStartButton()
        {
            startButton = new Button
            {
                Size = new Size(this.Width / 8, this.Height / 8),
                Location = new Point(this.Width - this.Width / 8 - 20, this.Height - this.Height / 8 - 20),
                Text = "START",
                Font = new Font("Consolas", 20, FontStyle.Regular),
                TextAlign = ContentAlignment.MiddleCenter,
            };
            startButton.Click += StartButton_Click;
            this.Controls.Add(startButton);
        }
        private void StartNewProcessAndClose()
        {
            Process.Start(Path.GetFullPath(subProcessPath));
            this.Close();
            Application.Exit();
        }
        private void StartButton_Click(object sender, EventArgs e)
        {
            StartNewProcessAndClose();
        }
        private void Form1_Load(object sender, EventArgs e)
        {
            CenterForm();
            AddProgressBar();
            AddProgressLabel();
            SimulateProgress();
        }
        private void OnFinishProgress()
        {
            this.Controls.Remove(progressLabel);
            this.Controls.Remove(progressBar);
            if (!autoStart)
            {
                AddStartButton();
            }
            else
            {
                StartNewProcessAndClose();
            }
        }

        private async void SimulateProgress()
        {
            progressLabel.Text = "Checking Content...";
            progressBar.Value = 0;
            await initializationTask;

            
            string json = File.ReadAllText(hashFile.Substring(1));
            dynamic hashInfo = JsonConvert.DeserializeObject(json);
            List<string> invalid_files_list = new List<string>();
            foreach (var key in hashInfo)
            {
                string name = key.Name.ToString();
                if (!name.Contains(comparePath + "/"))
                {
                    continue;
                }
                string hash = key.First.ToString();
                string fullPath = Path.GetFullPath(name.Substring(1));
                using (var md5 = MD5.Create())
                {
                    try
                    {
                        using (var stream = File.OpenRead(fullPath))
                        {
                            byte[] hashVal = md5.ComputeHash(stream);
                            string hashStr = BitConverter.ToString(hashVal).Replace("-", "").ToLower();
                            if (hashStr != hash)
                            {
                                invalid_files_list.Add(name);
                            }
                        }
                    } catch
                    {
                        invalid_files_list.Add(name);
                    }
                }
            }
            progressBar.Value = 1;
            var num_files_to_update = invalid_files_list.Count();
            for (int i = 0; i < num_files_to_update; i++)
            {
                progressLabel.Text = $"Downloading {i}/{num_files_to_update}: {invalid_files_list.ElementAt(i)}";
                await DownloadFileByPath(invalid_files_list.ElementAt(i));
                progressBar.Value = Math.Min(100, (int)(100 * (float)(i + 1) / (float)num_files_to_update + 1));
            }
            OnFinishProgress();
        }
    }
}