﻿using SimpleJSON;
using SongLoaderPlugin;
using SongLoaderPlugin.OverrideClasses;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace BeatSaverDownloader.Misc
{
    public class SongDownloader : MonoBehaviour
    {
        public event Action<Song> songDownloaded;

        private static SongDownloader _instance = null;
        public static SongDownloader Instance
        {
            get
            {
                if (!_instance)
                    _instance = new GameObject("SongDownloader").AddComponent<SongDownloader>();
                return _instance;
            }
            private set
            {
                _instance = value;
            }
        }

        private List<Song> _alreadyDownloadedSongs;
        private static bool _extractingZip;

        public void Awake()
        {
            DontDestroyOnLoad(gameObject);
            if (!SongLoader.AreSongsLoaded)
            {
                SongLoader.SongsLoadedEvent += SongLoader_SongsLoadedEvent;
            }
            else
            {
                SongLoader_SongsLoadedEvent(null, SongLoader.CustomLevels);
            }
        }

        private void SongLoader_SongsLoadedEvent(SongLoader sender, List<CustomLevel> levels)
        {
            _alreadyDownloadedSongs = levels.Select(x => new Song(x)).ToList();
        }

        public IEnumerator DownloadSongCoroutine(Song songInfo)
        {
            songInfo.songQueueState = SongQueueState.Downloading;

            UnityWebRequest www;
            bool timeout = false;
            float time = 0f;
            UnityWebRequestAsyncOperation asyncRequest;

            try
            {
                www = UnityWebRequest.Get(songInfo.downloadUrl);

                asyncRequest = www.SendWebRequest();
            }
            catch (Exception e)
            {
                Plugin.log.Error(e);
                songInfo.songQueueState = SongQueueState.Error;
                songInfo.downloadingProgress = 1f;

                yield break;
            }

            while ((!asyncRequest.isDone || songInfo.downloadingProgress < 1f) && songInfo.songQueueState != SongQueueState.Error)
            {
                yield return null;

                time += Time.deltaTime;

                if (time >= 5f && asyncRequest.progress <= float.Epsilon)
                {
                    www.Abort();
                    timeout = true;
                    Plugin.log.Error("Connection timed out!");
                }

                songInfo.downloadingProgress = asyncRequest.progress;
            }

            if(songInfo.songQueueState == SongQueueState.Error && (!asyncRequest.isDone || songInfo.downloadingProgress < 1f))
                www.Abort();

            if (www.isNetworkError || www.isHttpError || timeout || songInfo.songQueueState == SongQueueState.Error)
            {
                songInfo.songQueueState = SongQueueState.Error;
                Plugin.log.Error("Unable to download song! " + (www.isNetworkError ? $"Network error: {www.error}" : (www.isHttpError ? $"HTTP error: {www.error}" : "Unknown error")));
            }
            else
            {
                Plugin.log.Info("Received response from BeatSaver.com...");

                string docPath = "";
                string customSongsPath = "";

                byte[] data = www.downloadHandler.data;

                Stream zipStream = null;

                try
                {
                    docPath = Application.dataPath;
                    docPath = docPath.Substring(0, docPath.Length - 5);
                    docPath = docPath.Substring(0, docPath.LastIndexOf("/"));
                    customSongsPath = docPath + "/CustomSongs/" + songInfo.id + "/";
                    if (!Directory.Exists(customSongsPath))
                    {
                        Directory.CreateDirectory(customSongsPath);
                    }
                    zipStream = new MemoryStream(data);
                    Plugin.log.Info("Downloaded zip!");
                }
                catch (Exception e)
                {
                    Plugin.log.Critical(e);
                    songInfo.songQueueState = SongQueueState.Error;
                    yield break;
                }

                yield return new WaitWhile(() => _extractingZip); //because extracting several songs at once sometimes hangs the game

                Task extract = ExtractZipAsync(songInfo, zipStream, customSongsPath);
                yield return new WaitWhile(() => !extract.IsCompleted);
                songDownloaded?.Invoke(songInfo);
            }
        }

        private async Task ExtractZipAsync(Song songInfo, Stream zipStream, string customSongsPath)
        {
            try
            {
                Plugin.log.Info("Extracting...");
                _extractingZip = true;
                ZipArchive archive = new ZipArchive(zipStream, ZipArchiveMode.Read);
                await Task.Run(() => archive.ExtractToDirectory(customSongsPath)).ConfigureAwait(false);
                archive.Dispose();
            }
            catch (Exception e)
            {
                Plugin.log.Critical($"Unable to extract ZIP! Exception: {e}");
                songInfo.songQueueState = SongQueueState.Error;
                _extractingZip = false;
                return;
            }
            zipStream.Close();

            songInfo.path = Directory.GetDirectories(customSongsPath).FirstOrDefault();

            if (string.IsNullOrEmpty(songInfo.path))
            {
                songInfo.path = customSongsPath;
            }

            _extractingZip = false;
            songInfo.songQueueState = SongQueueState.Downloaded;
            _alreadyDownloadedSongs.Add(songInfo);
            Plugin.log.Info($"Extracted {songInfo.songName} {songInfo.songSubName}!");

            HMMainThreadDispatcher.instance.Enqueue(() => {
                try
                {
                    string dirName = new DirectoryInfo(customSongsPath).Name;
#if DEBUG
                    Logger.Log("Original path: " + customSongsPath);
                    Logger.Log("Folder name: " + dirName);
#endif

                    SongLoader.SongsLoadedEvent -= Plugin.instance.SongLoader_SongsLoadedEvent;
                    Action<SongLoader, List<CustomLevel>> songsLoadedAction = null;
                    songsLoadedAction = (arg1, arg2) =>
                    {
                        SongLoader.SongsLoadedEvent -= songsLoadedAction;
                        SongLoader.SongsLoadedEvent += Plugin.instance.SongLoader_SongsLoadedEvent;
                    };
                    SongLoader.SongsLoadedEvent += songsLoadedAction;

                    SongLoader.Instance.RetrieveNewSong(dirName);

                }
                catch (Exception e)
                {
                    Plugin.log.Critical("Unable to load song! Exception: " + e);
                }
            });
            
        }

        public void DeleteSong(Song song)
        {
            bool zippedSong = false;
            string path = "";

            CustomLevel level = SongLoader.CustomLevels.FirstOrDefault(x => x.levelID.StartsWith(song.hash));

            if (level != null)
                SongLoader.Instance.RemoveSongWithLevelID(level.levelID);

            if (string.IsNullOrEmpty(song.path))
            {
                if (level != null)
                    path = level.customSongInfo.path;
            }
            else
            {
                path = song.path;
            }

            if (string.IsNullOrEmpty(path))
                return;
            if (!Directory.Exists(path))
                return;

            if (path.Contains("/.cache/"))
                zippedSong = true;

            Task.Run(() =>
            {
                if (zippedSong)
                {
                    Plugin.log.Info("Deleting \"" + path.Substring(path.LastIndexOf('/')) + "\"...");

                    if (PluginConfig.deleteToRecycleBin)
                    {
                        FileOperationAPIWrapper.MoveToRecycleBin(path);
                    }
                    else
                    {
                        Directory.Delete(path, true);
                    }

                    string songHash = Directory.GetParent(path).Name;

                    try
                    {
                        if (Directory.GetFileSystemEntries(path.Substring(0, path.LastIndexOf('/'))).Length == 0)
                        {
                            Plugin.log.Info("Deleting empty folder \"" + path.Substring(0, path.LastIndexOf('/')) + "\"...");
                            Directory.Delete(path.Substring(0, path.LastIndexOf('/')), false);
                        }
                    }
                    catch
                    {
                        Plugin.log.Warn("Can't find or delete empty folder!");
                    }

                    string docPath = Application.dataPath;
                    docPath = docPath.Substring(0, docPath.Length - 5);
                    docPath = docPath.Substring(0, docPath.LastIndexOf("/"));
                    string customSongsPath = docPath + "/CustomSongs/";

                    string hash = "";

                    foreach (string file in Directory.GetFiles(customSongsPath, "*.zip"))
                    {
                        if (CreateMD5FromFile(file, out hash))
                        {
                            if (hash == songHash)
                            {
                                File.Delete(file);
                                break;
                            }
                        }
                    }

                }
                else
                {
                    Plugin.log.Info("Deleting \"" + path.Substring(path.LastIndexOf('/')) + "\"...");

                    if (PluginConfig.deleteToRecycleBin)
                    {
                        FileOperationAPIWrapper.MoveToRecycleBin(path);
                    }
                    else
                    {
                        Directory.Delete(path, true);
                    }

                    try
                    {
                        if (Directory.GetFileSystemEntries(path.Substring(0, path.LastIndexOf('/'))).Length == 0)
                        {
                            Plugin.log.Info("Deleting empty folder \"" + path.Substring(0, path.LastIndexOf('/')) + "\"...");
                            Directory.Delete(path.Substring(0, path.LastIndexOf('/')), false);
                        }
                    }
                    catch
                    {
                        Plugin.log.Warn("Unable to delete empty folder!");
                    }
                }

                Plugin.log.Info($"{_alreadyDownloadedSongs.RemoveAll(x => x.Compare(song))} song removed");
            }).ConfigureAwait(false);

            
        }

        public bool IsSongDownloaded(Song song)
        {
            if (SongLoader.AreSongsLoaded)
            {
                return _alreadyDownloadedSongs.Any(x => x.Compare(song));
            }
            else
                return false;
        }

        public static string GetLevelID(Song song)
        {
            string[] values = new string[] { song.hash, song.songName, song.songSubName, song.authorName, song.beatsPerMinute };
            return string.Join("∎", values) + "∎";
        }

        public static BeatmapLevelSO GetLevel(string levelId)
        {
            return SongLoader.CustomBeatmapLevelPackCollectionSO.beatmapLevelPacks.SelectMany(x => x.beatmapLevelCollection.beatmapLevels).FirstOrDefault(x => x.levelID == levelId) as BeatmapLevelSO;
        }

        public static bool CreateMD5FromFile(string path, out string hash)
        {
            hash = "";
            if (!File.Exists(path)) return false;
            using (MD5 md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(path))
                {
                    byte[] hashBytes = md5.ComputeHash(stream);

                    StringBuilder sb = new StringBuilder();
                    foreach (byte hashByte in hashBytes)
                    {
                        sb.Append(hashByte.ToString("X2"));
                    }

                    hash = sb.ToString();
                    return true;
                }
            }
        }

        public void RequestSongByLevelID(string levelId, Action<Song> callback)
        {
            StartCoroutine(RequestSongByLevelIDCoroutine(levelId, callback));
        }

        public IEnumerator RequestSongByLevelIDCoroutine(string levelId, Action<Song> callback)
        {
            UnityWebRequest wwwId = UnityWebRequest.Get($"{PluginConfig.beatsaverURL}/api/songs/search/hash/" + levelId);
            wwwId.timeout = 10;

            yield return wwwId.SendWebRequest();


            if (wwwId.isNetworkError || wwwId.isHttpError)
            {
                Plugin.log.Error(wwwId.error);
            }
            else
            {
#if DEBUG
                Logger.Log("Received response from BeatSaver...");
#endif
                JSONNode node = JSON.Parse(wwwId.downloadHandler.text);

                if (node["songs"].Count == 0)
                {
                    Plugin.log.Error($"Song {levelId} doesn't exist on BeatSaver!");
                    callback?.Invoke(null);
                    yield break;
                }

                Song _tempSong = Song.FromSearchNode(node["songs"][0]);
                callback?.Invoke(_tempSong);
            }
        }

        public void RequestSongByKey(string key, Action<Song> callback)
        {
            StartCoroutine(RequestSongByKeyCoroutine(key, callback));
        }

        public IEnumerator RequestSongByKeyCoroutine(string key, Action<Song> callback)
        {
            UnityWebRequest wwwId = UnityWebRequest.Get($"{PluginConfig.beatsaverURL}/api/songs/detail/" + key);
            wwwId.timeout = 10;

            yield return wwwId.SendWebRequest();


            if (wwwId.isNetworkError || wwwId.isHttpError)
            {
                Plugin.log.Error(wwwId.error);
            }
            else
            {
#if DEBUG
                Logger.Log("Received response from BeatSaver...");
#endif
                JSONNode node = JSON.Parse(wwwId.downloadHandler.text);

                Song _tempSong = new Song(node["song"]);
                callback?.Invoke(_tempSong);
            }
        }
    }
}
