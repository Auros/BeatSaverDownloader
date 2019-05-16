﻿using BeatSaverDownloader.UI.UIElements;
using CustomUI.BeatSaber;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using VRUI;

namespace BeatSaverDownloader.UI.ViewControllers
{
    class SearchKeyboardViewController : VRUIViewController
    {

        GameObject _searchKeyboardGO;

        CustomUIKeyboard _searchKeyboard;

        TextMeshProUGUI _inputText;
        public string _inputString = "";

        public event Action<string> searchButtonPressed;
        public event Action backButtonPressed;

        protected override void DidActivate(bool firstActivation, ActivationType type)
        {
            if (type == ActivationType.AddedToHierarchy && firstActivation)
            {
                _searchKeyboardGO = Instantiate(Resources.FindObjectsOfTypeAll<UIKeyboard>().First(x => x.name != "CustomUIKeyboard"), rectTransform, false).gameObject;

                Destroy(_searchKeyboardGO.GetComponent<UIKeyboard>());
                _searchKeyboard = _searchKeyboardGO.AddComponent<CustomUIKeyboard>();

                _searchKeyboard.textKeyWasPressedEvent += delegate (char input) { _inputString += input; UpdateInputText(); };
                _searchKeyboard.deleteButtonWasPressedEvent += delegate () { _inputString = _inputString.Substring(0, _inputString.Length - 1); UpdateInputText(); };
                _searchKeyboard.cancelButtonWasPressedEvent += () => { backButtonPressed?.Invoke(); };
                _searchKeyboard.okButtonWasPressedEvent += () => { searchButtonPressed?.Invoke(_inputString); };

                _inputText = BeatSaberUI.CreateText(rectTransform, "Search...", new Vector2(0f, 22f));
                _inputText.alignment = TextAlignmentOptions.Center;
                _inputText.fontSize = 6f;

            }
            else
            {
                _inputString = "";
                UpdateInputText();
            }

        }

        void UpdateInputText()
        {
            if (_inputText != null)
            {
                _inputText.text = _inputString?.ToUpper() ?? "";
                if (string.IsNullOrEmpty(_inputString))
                {
                    _searchKeyboard.OkButtonInteractivity = false;
                }
                else
                {
                    _searchKeyboard.OkButtonInteractivity = true;
                }
            }
        }

        void ClearInput()
        {
            _inputString = "";
        }
    }
}
