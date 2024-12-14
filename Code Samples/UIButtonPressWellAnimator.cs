using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using RedemptionGames.Constants;
using RedemptionGames.Utilities;
using RedemptionSDK.Core.Attributes;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Redemption.Scripts.UI
{
    [RequireComponent(typeof(Button))]
    public class ButtonPressWellAnimator : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        //swapping the sprite is handled by the button states
        [SerializeField] private RectTransform _baseTransform;
        [SerializeField] private RectTransform _buttonImageTransform;
        [SerializeField] private RectTransform _insideParentTransform;
        [SerializeField] private ButtonHaptics _haptics = new ();
        [SerializeField] private ButtonAudio _audio = new ();
        
        //basing distance to move off of %, not a fixed number, so when the buttons are larger it still looks correct
        private float _distance;
        [SerializeField] private float _distanceScaler = 0.030f;
        
        private float _startFontSize;
        private Dictionary<TextMeshProUGUI, bool> _textAutoSizing;
        private Button _button;
        private Vector2 _imageOffsetMax;
        private Vector2 _textOffsetMin;
        private Vector2 _textOffsetMax;
        
        private Vector2 _pointerDownPos;
        private Vector2 _pointerUpPos;
        private bool _wasDraggedAndEnabled;
        
        private void Start()
        {
            ActionButton.CheckAndDisableActionButtonMedia(gameObject);
            _button = GetComponent<Button>();
            
            // cache original values
            if (_buttonImageTransform != null)
            {
                _imageOffsetMax = _buttonImageTransform.offsetMax;
            }
            if (_insideParentTransform != null)
            {
                _textOffsetMin = _insideParentTransform.offsetMin;
                _textOffsetMax = _insideParentTransform.offsetMax;
            }
        }
        private void TryResetAnimationValues()
        {
            if (_buttonImageTransform != null)
            {
                _buttonImageTransform.offsetMax = _imageOffsetMax;
            }
            if (_insideParentTransform != null)
            {
                _insideParentTransform.offsetMin = _textOffsetMin;
                _insideParentTransform.offsetMax = _textOffsetMax;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            TryResetAnimationValues();
            
            if (HasDisabledButton())
            {   // don't trigger animation if the button is not interactable or enabled
                return;
            }
            
            if (_baseTransform == null)
            {
                _baseTransform = GetComponent<RectTransform>();
            }

            if (_baseTransform == null)
            {
                return;
            }
                
            _distance = _baseTransform.rect.height * _distanceScaler;
            
            _pointerDownPos = eventData.position;
            _pointerUpPos = _pointerDownPos;
            
            _textAutoSizing = new Dictionary<TextMeshProUGUI, bool>();
            
            foreach (var text in GetComponentsInChildren<TextMeshProUGUI>())
            {
                if (!text.enableAutoSizing)
                {
                    continue;
                }
                
                float fontSize = text.fontSize;
                _textAutoSizing.Add(text, text.enableAutoSizing);
                text.enableAutoSizing = false;
                text.fontSize = fontSize;
            }
            
            if(_buttonImageTransform != null)
            {
                var imageOffsetMax = _buttonImageTransform.offsetMax;
                imageOffsetMax = new Vector2(imageOffsetMax.x, imageOffsetMax.y - _distance);
                _buttonImageTransform.offsetMax = imageOffsetMax;
            }

            if (_insideParentTransform != null)
            {
                var textOffsetMin = _insideParentTransform.offsetMin;
                textOffsetMin = new Vector2(textOffsetMin.x, textOffsetMin.y - _distance);
                _insideParentTransform.offsetMin = textOffsetMin;

                var textOffsetMax = _insideParentTransform.offsetMax;
                textOffsetMax = new Vector2(textOffsetMax.x, textOffsetMax.y - _distance);
                _insideParentTransform.offsetMax = textOffsetMax;
            }
        }
        
        public void OnPointerUp(PointerEventData eventData)
        {
            if (HasDisabledButton())
            {   // don't trigger animation if the button is not interactable or enabled
                return;
            }
            
            _pointerUpPos = eventData.position;
            
            if (RGConstants.OverDragDistance(_pointerDownPos, _pointerUpPos))
            {
                DisableFromDrag().Forget();
                return;
            }

            _haptics?.TryPlay();
            _audio?.TryPlay();
            ButtonUpAnim();
        }
        
        private void ButtonUpAnim()
        {
            if (_textAutoSizing is { Count: > 0 })
            {
                foreach (var text in GetComponentsInChildren<TextMeshProUGUI>())
                {
                    if (_textAutoSizing.ContainsKey(text))
                    {
                        text.enableAutoSizing = true;
                    }
                }
            }
            
            if(_buttonImageTransform != null)
            {
                var imageOffsetMax = _buttonImageTransform.offsetMax;
                imageOffsetMax = new Vector2(imageOffsetMax.x, imageOffsetMax.y + _distance);
                _buttonImageTransform.offsetMax = imageOffsetMax;
            }

            if (_insideParentTransform != null)
            {
                var textOffsetMin = _insideParentTransform.offsetMin;
                textOffsetMin = new Vector2(textOffsetMin.x, textOffsetMin.y + _distance);
                _insideParentTransform.offsetMin = textOffsetMin;

                var textOffsetMax = _insideParentTransform.offsetMax;
                textOffsetMax = new Vector2(textOffsetMax.x, textOffsetMax.y + _distance);
                _insideParentTransform.offsetMax = textOffsetMax;
            }
        }

        private async UniTask DisableFromDrag()
        {
            _button.interactable = false;
            
            //reset animation
            ButtonUpAnim();
            
            await UniTask.NextFrame();
            
            _button.interactable = true;
        }
        
        private bool HasDisabledButton()
        {
            if (_button == null)
            {   // try get button component only when required (to reduce component startup time)
                _button = GetComponent<Button>();
            }

            if (_button != null && (!_button.enabled || !_button.interactable))
            {
                return true;
            }

            return false;
        }

        [Button]
        private void TestPointerDown()
        {
            OnPointerDown(null);
        }
        
        [Button]
        private void TestPointerUp()
        {
            OnPointerUp(null);
        }
    }
}
