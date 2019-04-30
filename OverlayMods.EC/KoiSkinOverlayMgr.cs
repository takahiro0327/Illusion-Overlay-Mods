﻿using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using KKAPI;
using KKAPI.Chara;
using OverlayMods;
using UnityEngine;
using Resources = KoiSkinOverlayX.Properties.Resources;

namespace KoiSkinOverlayX
{
    [BepInPlugin(GUID, "ECSOX (EC SkinOverlay)", Version)]
    [BepInDependency("com.bepis.bepinex.extendedsave")]
    [BepInDependency(KoikatuAPI.GUID)]
    public class KoiSkinOverlayMgr : BaseUnityPlugin
    {
        public const string GUID = Metadata.GUID_KSOX;
        internal const string Version = Metadata.Version;
        
        private static ConfigWrapper<bool> CompressTextures { get; set; }
        private static ConfigWrapper<string> ExportDirectory { get; set; }

        private static readonly string _defaultOverlayDirectory = Path.Combine(Paths.GameRootPath, "UserData\\Overlays");
        public static string OverlayDirectory
        {
            get
            {
                var path = ExportDirectory.Value;
                return Directory.Exists(path) ? path : _defaultOverlayDirectory;
            }
        }

        internal static Material OverlayMat { get; private set; }
        private static ManualLogSource _logger;

        private void Awake()
        {
            _logger = Logger;

            ExportDirectory = Config.Wrap("", "Overlay export/open folder", "The value needs to be a valid full path to an existing folder. Default folder will be used if the value is invalid. Exported overlays will be saved there, and by default open overlay dialog will show this directory.", _defaultOverlayDirectory);
            CompressTextures = Config.Wrap("", "Compress overlay textures in RAM", "Reduces RAM usage to about 1/4th at the cost of lower quality. Use when loading lots of characters with overlays if you're running out of memory.", false);

            KoikatuAPI.CheckRequiredPlugin(this, KoikatuAPI.GUID, new Version(KoikatuAPI.VersionConst));

            var ab = AssetBundle.LoadFromMemory(Resources.composite);
            OverlayMat = new Material(ab.LoadAsset<Shader>("assets/composite.shader"));
            DontDestroyOnLoad(OverlayMat);
            ab.Unload(false);

            Hooks.Init();
            CharacterApi.RegisterExtraBehaviour<KoiSkinOverlayController>(GUID);

            Directory.CreateDirectory(OverlayDirectory);
        }

        public static TextureFormat GetSelectedOverlayTexFormat()
        {
            return CompressTextures.Value ? TextureFormat.DXT5 : TextureFormat.ARGB32;
        }

        internal static string GetTexFilename(string charFullname, TexType texType)
        {
            string name;

            switch (texType)
            {
                case TexType.BodyOver:
                    name = "Body";
                    break;
                case TexType.FaceOver:
                    name = "Face";
                    break;
                case TexType.Unknown:
                    return null;
                default:
                    name = texType.ToString();
                    break;
            }

            var legacyDir = Path.Combine(Paths.PluginPath, "KoiSkinOverlay");
            var charFolder = $"{legacyDir}/{charFullname}";
            var texFilename = $"{charFolder}/{name}.png";
            return texFilename;
        }

        /// <summary>
        /// Old loading logic from folders
        /// </summary>
        internal static byte[] GetOldStyleOverlayTex(TexType texType, ChaControl chaControl)
        {
            var charFullname = chaControl.fileParam?.fullname;
            if (!string.IsNullOrEmpty(charFullname))
            {
                var texFilename = GetTexFilename(charFullname, texType);

                if (File.Exists(texFilename))
                {
                    Log(LogLevel.Info, $"[KSOX] Importing texture data for {charFullname} from file {texFilename}");

                    try
                    {
                        var fileTexBytes = File.ReadAllBytes(texFilename);
                        var overlayTex = Util.TextureFromBytes(fileTexBytes, TextureFormat.ARGB32);
                        // todo re-convert the texture, check for size
                        if (overlayTex != null)
                            return overlayTex.EncodeToPNG();
                    }
                    catch (Exception ex)
                    {
                        Log(LogLevel.Error, "[KSOX] Failed to load texture from file - " + ex.Message);
                    }
                }
            }
            return null;
        }

        internal static void Log(LogLevel logLevel, object data)
        {
            _logger.Log(logLevel, data);
        }
    }
}
