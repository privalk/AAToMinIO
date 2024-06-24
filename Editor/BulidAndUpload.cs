using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using GluonGui.Dialog;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace Dropletonverse.Infra.AssetSync
{
    public class BuildAndUpload : EditorWindow
    {
        private const string THIS_PACKAGE_NAME = "dropletonverse.infra.assetsync";
        private const string SELECTED_BUCKET_PREF_KEY = "SelectedBucketName";

        private static readonly string DefaultEndpoint = "minio.dropletonverse.com:33355";
        private static readonly string DefaultAccessKey = "minioadmin";
        private static readonly string DefaultSecretKey = "minioadmin";

        private static string endpoint = DefaultEndpoint;
        private static string accessKey = DefaultAccessKey;
        private static string secretKey = DefaultSecretKey;
        private static string productName = "";
        private static string version = "";
        private static string bucketName;

        private IMinioClient minioClient;
        private List<string> bucketNames = new List<string>();
        private int selectedBucketIndex;

        [MenuItem("Dropletonverse Infra/Asset Sync/Build Assets and Upload")]
        public static void ShowWindow()
        {
            GetWindow<BuildAndUpload>("Build And Upload");
        }

        private async void OnEnable()
        {
            InitMinioClient();
            await RefreshBuckets();

            if (EditorPrefs.HasKey(SELECTED_BUCKET_PREF_KEY))
            {
                string savedBucketName = EditorPrefs.GetString(SELECTED_BUCKET_PREF_KEY);
                selectedBucketIndex = bucketNames.IndexOf(savedBucketName);
                if (selectedBucketIndex >= 0)
                {
                    bucketName = bucketNames[selectedBucketIndex];
                }
            }
        }

        private void OnGUI()
        {
            DrawAssetSyncPreferences();

            if (bucketNames.Count > 0)
            {
                int newSelectedBucketIndex = EditorGUILayout.Popup("Bucket Name", selectedBucketIndex, bucketNames.ToArray());
                if (newSelectedBucketIndex != selectedBucketIndex)
                {
                    selectedBucketIndex = newSelectedBucketIndex;
                    bucketName = bucketNames[selectedBucketIndex];
                    EditorPrefs.SetString(SELECTED_BUCKET_PREF_KEY, bucketName);
                }
            }
            else
            {
                EditorGUILayout.LabelField("No buckets available.");
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh Buckets"))
            {
                _ = RefreshBuckets();
            }
            if (GUILayout.Button("Create New Bucket"))
            {
                CreateNewBucketWindow();
            }
            EditorGUILayout.EndHorizontal();

            DrawProductAndVersionFields();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Build and Upload"))
            {
                _ = BuildAndUploadAssets();
            }

            if (GUILayout.Button("Update and Upload"))
            {
                UpdateAPreviousBuildAndUpload();
            }
            EditorGUILayout.EndHorizontal();
        }
        private void UpdateAPreviousBuildAndUpload()
        {

            var buildPath = ContentUpdateScript.GetContentStateDataPath(false);

            List<AddressableAssetEntry> entrys = ContentUpdateScript.GatherModifiedEntries(AddressableAssetSettingsDefaultObject.Settings, buildPath);


            AddressablesPlayerBuildResult result = ContentUpdateScript.BuildContentUpdate(AddressableAssetSettingsDefaultObject.Settings, buildPath);
            Debug.Log("BuildFinish path = " + AddressableAssetSettingsDefaultObject.Settings.RemoteCatalogBuildPath.GetValue(AddressableAssetSettingsDefaultObject.Settings));

        }
       
        private void DrawAssetSyncPreferences()
        {
            EditorGUILayout.BeginHorizontal();
            PreferenceField(ref endpoint, "Endpoint", DefaultEndpoint);
            if (GUILayout.Button("Link"))
            {
                InitMinioClient();
                _ = RefreshBuckets();
            }
            EditorGUILayout.EndHorizontal();
        }

        private static void PreferenceField(ref string target, string label, string defaultValue)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(100));
            target = EditorGUILayout.TextField(target, GUILayout.ExpandWidth(true));
            if (string.IsNullOrEmpty(target))
            {
                target = defaultValue;
            }
            EditorGUILayout.EndHorizontal();
        }

        private void InitMinioClient()
        {
            try
            {
                minioClient = new MinioClient()
                    .WithEndpoint(endpoint)
                    .WithCredentials(accessKey, secretKey)
                    .Build();
            }
            catch (MinioException ex)
            {
                Debug.LogError($"MinIO Error: {ex.Message}");
            }
            catch (Exception)
            {
                Debug.LogError("Connection failed");
            }
        }

        private async Task RefreshBuckets()
        {
            try
            {
                var bucketList = await minioClient.ListBucketsAsync().ConfigureAwait(false);
                bucketNames.Clear();
                foreach (var bucket in bucketList.Buckets)
                {
                    bucketNames.Add(bucket.Name);
                }
                if (bucketNames.Count > 0)
                {
                    selectedBucketIndex = Math.Min(selectedBucketIndex, bucketNames.Count - 1);
                    bucketName = bucketNames[selectedBucketIndex];
                }
                EditorApplication.delayCall += Repaint;
            }
            catch (MinioException ex)
            {
                Debug.LogError(ex.Message);
            }
            catch (Exception)
            {
                Debug.LogError("Connection failed. Invalid endpoint or no buckets available.");
            }
        }

        private void CreateNewBucketWindow()
        {
            var window = GetWindow<CreateBucketWindow>("Create New Bucket");
            window.Init(this);
        }

        public async void CreateNewBucket(string newBucketName)
        {
            if (string.IsNullOrEmpty(newBucketName))
            {
                Debug.LogError("Bucket name cannot be empty.");
                return;
            }

            try
            {
                await minioClient.MakeBucketAsync(new MakeBucketArgs().WithBucket(newBucketName)).ConfigureAwait(false);
                var policyJson = $@"{{""Version"":""2012-10-17"",""Statement"":[{{""Action"":[""s3:GetObject""],""Effect"":""Allow"",""Principal"":{{""AWS"":[""*""]}},""Resource"":[""arn:aws:s3:::{newBucketName}/*""]}}]}}";
                var setPolicyArgs = new SetPolicyArgs()
                    .WithBucket(newBucketName)
                    .WithPolicy(policyJson);
                await minioClient.SetPolicyAsync(setPolicyArgs).ConfigureAwait(false);

                bucketNames.Add(newBucketName);
                selectedBucketIndex = bucketNames.Count - 1;
                bucketName = newBucketName;
                EditorPrefs.SetString(SELECTED_BUCKET_PREF_KEY, bucketName);

                Debug.Log("Bucket created successfully.");
                EditorApplication.delayCall += Repaint;
            }
            catch (MinioException ex)
            {
                Debug.LogError(ex.Message);
            }
        }

        private void DrawProductAndVersionFields()
        {
            EditorGUI.BeginChangeCheck();
            PreferenceField(ref productName, "ProductName", PlayerSettings.productName);
            PreferenceField(ref version, "Version", PlayerSettings.bundleVersion);
            if (EditorGUI.EndChangeCheck())
            {
                PlayerSettings.productName = productName;
                PlayerSettings.bundleVersion = version;
                AddressableAssetSettingsDefaultObject.Settings.OverridePlayerVersion = productName;
            }
        }

        private async Task BuildAndUploadAssets()
        {
            InitMinioClient();
            ModifyRemotePath();

            string projectRootPath = Path.GetDirectoryName(Application.dataPath);

            string directoryPath = Path.Combine(projectRootPath, "ServerData");

            // ClearDirectory(directoryPath);

            foreach (AddressableAssetGroup group in AddressableAssetSettingsDefaultObject.Settings.groups)
            {
                BundledAssetGroupSchema schema = group.GetSchema<BundledAssetGroupSchema>();
                if (schema != null)
                {
                    schema.BundleNaming = BundledAssetGroupSchema.BundleNamingStyle.NoHash;
                }
            }
            AddressableAssetSettings.BuildPlayerContent();

            try
            {
                if (!await minioClient.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucketName)).ConfigureAwait(false))
                {
                    await minioClient.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucketName)).ConfigureAwait(false);
                }
                await UploadDirectory(directoryPath);
            }
            catch (Exception ex)
            {
                Debug.LogError(ex.Message);
            }
        }

        // private void ClearDirectory(string directoryPath)
        // {
        //     if (Directory.Exists(directoryPath))
        //     {
        //         DirectoryInfo directory = new DirectoryInfo(directoryPath);
        //         foreach (FileInfo file in directory.GetFiles())
        //         {
        //             try
        //             {
        //                 if (!file.Extension.Equals(".bundle", StringComparison.OrdinalIgnoreCase))
        //                 {
        //                     file.Delete();
        //                 }
        //             }
        //             catch (Exception ex)
        //             {
        //                 Debug.LogError($"Failed to delete file {file.FullName}: {ex.Message}");
        //             }
        //         }
        //         foreach (DirectoryInfo subDirectory in directory.GetDirectories())
        //         {
        //             try
        //             {
        //                 subDirectory.Delete(true);
        //             }
        //             catch (Exception ex)
        //             {
        //                 Debug.LogError($"Failed to delete directory {subDirectory.FullName}: {ex.Message}");
        //             }
        //         }
        //     }
        // }

        private async Task UploadDirectory(string directoryPath)
        {
            var files = Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories);
            foreach (var filePath in files)
            {
                var relativePath = Path.GetRelativePath(directoryPath, filePath);
                relativePath = $"/{productName}/{version}/{relativePath.Replace("\\", "/")}";

                var putObjectArgs = new PutObjectArgs()
                    .WithBucket(bucketName)
                    .WithObject(relativePath)
                    .WithFileName(filePath);
                await minioClient.PutObjectAsync(putObjectArgs);
                Debug.Log($"{relativePath} uploaded successfully.");
            }
        }

        private void ModifyRemotePath()
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            settings.profileSettings.SetValue(settings.profileSettings.GetProfileId("Default"), "Remote.LoadPath", $"http://{endpoint}/{bucketName}/{productName}/{version}/[BuildTarget]");
        }
    }

    public class CreateBucketWindow : EditorWindow
    {
        private string newBucketName = string.Empty;
        private BuildAndUpload parentWindow;

        public void Init(BuildAndUpload parent)
        {
            parentWindow = parent;
        }

        private void OnGUI()
        {
            newBucketName = EditorGUILayout.TextField("New Bucket Name", newBucketName);

            if (GUILayout.Button("Create"))
            {
                parentWindow.CreateNewBucket(newBucketName);
                Close();
            }
        }
    }
}
