
using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using Minio;
using Minio.DataModel.Args;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace Dropletonverse.Infra.AssetSync
{
    public class AssetSyncWindow : EditorWindow
    {
        const string THIS_PACKAGE_NAME = "dropletonverse.infra.assetsync";

        static string endpoint;
        static string accessKey;
        static string secretKey;
        static string bucketName;
        private IMinioClient minioClient;

        [MenuItem("p/Asset Sync/Build Assets and Upload %&U")]
        public static void ShowWindow()
        {
            GetWindow<AssetSyncWindow>("Asset Sync");
        }

        public void OnGUI()
        {
            AssetSyncPreference();
            if (GUILayout.Button("Build and Upload"))
            {
                BuildAndUpload();
            }
        }

        static void AssetSyncPreference()
        {
            EditorGUI.BeginChangeCheck();

            PreferenceField(ref endpoint, "Endpoit", "172.16.16.156:9000");
            PreferenceField(ref accessKey, "AcceessKey", "minioadmin");
            PreferenceField(ref secretKey, "SecretKey", "minioadmin");
            PreferenceField(ref bucketName, "BucketName", "666");

            if (EditorGUI.EndChangeCheck())
            {
            }
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
        public async void BuildAndUpload()
        {
            //初始化minio
            minioClient = new MinioClient()
           .WithEndpoint(endpoint)
           .WithCredentials(accessKey, secretKey)
           .Build();

            // 构建Addressables
            AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult addressablesPlayerBuildResult);
            List<string> BundleFilePath = new();
            foreach (var item in addressablesPlayerBuildResult.AssetBundleBuildResults)
            {
                BundleFilePath.Add(item.FilePath);
            }

            try
            {
                //查找桶
                var BExistsArgs = new BucketExistsArgs()
                .WithBucket(bucketName);
                var found = await minioClient.BucketExistsAsync(BExistsArgs).ConfigureAwait(false);
                //没有该桶则创建再上传
                if (!found)
                {
                    await minioClient.MakeBucketAsync(
                        new MakeBucketArgs()
                         .WithBucket(bucketName)
                        ).ConfigureAwait(false);

                    Upload(BundleFilePath);
                }
                //有则上传
                else
                {
                    Upload(BundleFilePath);

                }
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
            }

        }
        public async void Upload(List<string> BundleFilePath)
        {
            foreach (var item in BundleFilePath)
            {
                var BPutargs = new PutObjectArgs()
                                .WithBucket(bucketName)
                                .WithObject(GetName(item))
                                .WithFileName(item);
                _ = await minioClient.PutObjectAsync(BPutargs).ConfigureAwait(false);

                Debug.Log("上传成功" + item);
            }
        }

        public string GetName(string fullPath)
        {
            // 查找最后一个 '\' 的索引
            int lastSlashIndex = fullPath.LastIndexOf('\\');

            // 如果找到了 '\'
            if (lastSlashIndex != -1)
            {
                // 提取 '\' 之后的内容
                string contentAfterSlash = fullPath[(lastSlashIndex + 1)..];

                // 输出结果
                return contentAfterSlash;
            }
            else
            {
                return fullPath;
            }

        }
    }

}

