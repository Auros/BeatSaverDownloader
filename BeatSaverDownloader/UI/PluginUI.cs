﻿using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using BeatSaverDownloader.UI.FlowCoordinators;
using SongLoaderPlugin;
using SongLoaderPlugin.OverrideClasses;
using CustomUI.BeatSaber;
using CustomUI.MenuButton;
using TMPro;
using BeatSaverDownloader.Misc;
using System;
using CustomUI.Settings;
using System.Collections;

namespace BeatSaverDownloader.UI
{
    class PluginUI : MonoBehaviour
    {
        public bool initialized = false;

        private static PluginUI _instance = null;
        public static PluginUI Instance
        {
            get
            {
                if (!_instance)
                {
                    _instance = new GameObject("BeatSaverDownloader").AddComponent<PluginUI>();
                    DontDestroyOnLoad(_instance.gameObject);
                }
                return _instance;
            }
            private set
            {
                _instance = value;
            }
        }

        public MoreSongsFlowCoordinator moreSongsFlowCoordinator;
        public MorePlaylistsFlowCoordinator morePlaylistsFlowCoordinator;
        public ReviewFlowCoordinator reviewFlowCoordinator;

        private MenuButton _moreSongsButton;

        public void OnLoad()
        {
            initialized = false;

            StartCoroutine(SetupUI());

            if (!SongLoader.AreSongsLoaded)
                SongLoader.SongsLoadedEvent += SongLoader_SongsLoadedEvent;
            else
                SongLoader_SongsLoadedEvent(null, SongLoader.CustomLevels);

            StartCoroutine(ScrappedData.Instance.DownloadScrappedData((List<ScrappedSong> songs) => {
                if (PlaylistsCollection.loadedPlaylists.Any(x => x.playlistTitle == "Your favorite songs"))
                {
                    PlaylistsCollection.loadedPlaylists.First(x => x.playlistTitle == "Your favorite songs").SavePlaylist("Playlists\\favorites.json");
                }
            }));
        }

        private void SongLoader_SongsLoadedEvent(SongLoader arg1, List<CustomLevel> arg2)
        {
            SongLoader.SongsLoadedEvent -= SongLoader_SongsLoadedEvent;
            _moreSongsButton.interactable = true;
        }

        private IEnumerator SetupUI()
        {
            if (initialized) yield break;

            var downloaderSubMenu = SettingsUI.CreateSubMenu("Downloader");

            var disableDeleteButton = downloaderSubMenu.AddBool("Disable delete button");
            disableDeleteButton.GetValue += delegate { return PluginConfig.disableDeleteButton; };
            disableDeleteButton.SetValue += delegate (bool value) { PluginConfig.disableDeleteButton = value; PluginConfig.SaveConfig(); };

            var deleteToRecycleBin = downloaderSubMenu.AddBool("Delete to Recycle Bin");
            deleteToRecycleBin.GetValue += delegate { return PluginConfig.deleteToRecycleBin; };
            deleteToRecycleBin.SetValue += delegate (bool value) { PluginConfig.deleteToRecycleBin = value; PluginConfig.SaveConfig(); };

            var enableSongIcons = downloaderSubMenu.AddBool("Enable additional song icons");
            enableSongIcons.GetValue += delegate { return PluginConfig.enableSongIcons; };
            enableSongIcons.SetValue += delegate (bool value) { PluginConfig.enableSongIcons = value; PluginConfig.SaveConfig(); };

            var rememberLastPackAndSong = downloaderSubMenu.AddBool("Remember last pack and song");
            rememberLastPackAndSong.GetValue += delegate { return PluginConfig.rememberLastPackAndSong; };
            rememberLastPackAndSong.SetValue += delegate (bool value) { PluginConfig.rememberLastPackAndSong = value; PluginConfig.SaveConfig(); };

            var maxSimultaneousDownloads = downloaderSubMenu.AddInt("Max simultaneous downloads", 1, 10, 1);
            maxSimultaneousDownloads.GetValue += delegate { return PluginConfig.maxSimultaneousDownloads; };
            maxSimultaneousDownloads.SetValue += delegate (int value) { PluginConfig.maxSimultaneousDownloads = value; PluginConfig.SaveConfig(); };

            var fastScrollSpeed = downloaderSubMenu.AddInt("Fast scroll speed", 2, 20, 1);
            fastScrollSpeed.GetValue += delegate { return PluginConfig.fastScrollSpeed; };
            fastScrollSpeed.SetValue += delegate (int value) { PluginConfig.fastScrollSpeed = value; PluginConfig.SaveConfig(); };

            _moreSongsButton = MenuButtonUI.AddButton("More songs", "Download more songs from BeatSaver.com!", BeatSaverButtonPressed);
            _moreSongsButton.interactable = SongLoader.AreSongsLoaded;

            MenuButtonUI.AddButton("More playlists", PlaylistsButtonPressed);

            yield return null;

            initialized = true;
        }

        public void BeatSaverButtonPressed()
        {
            if (moreSongsFlowCoordinator == null)
                moreSongsFlowCoordinator = new GameObject("MoreSongsFlowCoordinator").AddComponent<MoreSongsFlowCoordinator>();

            MainFlowCoordinator mainFlow = Resources.FindObjectsOfTypeAll<MainFlowCoordinator>().First();

            mainFlow.InvokeMethod("PresentFlowCoordinator", moreSongsFlowCoordinator, null, false, false);
        }

        public void PlaylistsButtonPressed()
        {
            if (morePlaylistsFlowCoordinator == null)
                morePlaylistsFlowCoordinator = new GameObject("MorePlaylistsFlowCoordinator").AddComponent<MorePlaylistsFlowCoordinator>();

            MainFlowCoordinator mainFlow = Resources.FindObjectsOfTypeAll<MainFlowCoordinator>().First();

            mainFlow.InvokeMethod("PresentFlowCoordinator", morePlaylistsFlowCoordinator, null, false, false);
        }

    }
}
