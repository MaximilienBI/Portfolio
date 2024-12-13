using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Internal.UITools.SimplePlayableAnimController;
using RedemptionSDK.Core.UI.UIManager;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using I2.Loc;
using Internal.UITools.StateEventInvoker;
using Quantum;
using RedemptionGames.Utilities;
using RedemptionSDK.AssetManager;
using RedemptionSDK.Core;
using RedemptionSDK.Core.Player;
using UnityEngine.Playables;

public class UISelectedCardPopup : GameUIElementPopup
{
    [Header("Panels")]
    [SerializeField] private GameObject _skillPanel;
    [SerializeField] private GameObject _statPanel;
    [SerializeField] private ScrollRect _scrollRect;
    [SerializeField] private GameObject _skillPanelPip;
    [SerializeField] private GameObject _statPanelPip;
    
    [Header("Stats Config")]
    [SerializeField] private TextMeshProUGUI _name;
    [SerializeField] private TextMeshProUGUI _rarity;
    [SerializeField] private StateEventInvoker _rarityStateEventInvoker;
    [SerializeField] private TextMeshProUGUI _skillName;
    [SerializeField] private TextMeshProUGUI _skillDescription;
    [SerializeField] private TextMeshProUGUI _level;
    [SerializeField] private Slider _xpBar;
    [SerializeField] private Image _xpBarSliderImage;
    [SerializeField] private TextMeshProUGUI _xpText;
    [SerializeField] private TextMeshProUGUI _statsTitle;
    [SerializeField] private UICharacterStatController[] _stats;
    [SerializeField] private UICharacterStatShorthandController[] _statsShorthand;

    [Header("Visuals Config")]
    [SerializeField] private ImageLoader _characterImage;
    [SerializeField] private ImageLoader _abilityImage;
    [SerializeField] private SimplePlayableAnimController _animController;
    [SerializeField] private GameObject[] _backgroundColors; //update with vfx
    [SerializeField] private StateEventInvoker _updradeStateEventInvoker;

    [Header("Stat Base Colors")]
    [SerializeField] private UICharacterStatController.StatStylingData _baseStatStyle;
    [SerializeField] private UICharacterStatController.StatStylingData _upgradedStatStyle;
    
    
    [Header("TopBar Anchor Config")]
    [SerializeField] private RectTransform _contentRect;
    [SerializeField] private float _topBarHeightOffset = -300f;
    
    [Header("Upgrades Config")]
    [SerializeField] private UIUpgradeCardButtonController _upgradeButton;
    [SerializeField] private Ease _toNextLevelTweenType = Ease.OutCubic;
    [SerializeField] private float _toNextLevelTweenTime = 0.5f;
    [SerializeField] private SimplePlayableAnimController _upgradeAnimPlayer;
    private float _animDuration = 0.15f;
    private float _animDelayDuration = 0.75f;

    [Header("Use Button")] 
    [SerializeField] private GameObject _buttonParent;
    [SerializeField] private GameObject _useButton;
    [SerializeField] private GameObject _useButtonParent;
    [SerializeField] private GameObject _notOwnedButton;
    [SerializeField] private TextMeshProUGUI _arenaUnlockText;

    [Header("Ghost Text")]
    [SerializeField] private TextMeshProUGUI _ghostText;
    [SerializeField] private PlayableDirector _ghostTextDirector;

    private CardData _currentCardData;
    private Vector2 _touchDelta;
    private Tweener _scrollTween;
    
    private bool _isCardInDeck;
    
    // Localization
    private readonly I2CompoundHelper _locHelper = new I2CompoundHelper();
    
    public void SetupCardPopup(CardData cardData, bool isCardInDeck, bool showButtons)
    {
        SetupTopAnchor(); //have to always set up top anchor because otherwise animation messes up
        
        _isCardInDeck = isCardInDeck;
        _skillPanel.SetActive(true);
        _statPanel.SetActive(true);
        SetupUI(cardData, showButtons);

        // Hide canvas until it's animated to no longer be transparent
        var canvasGroup = gameObject.GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0;
        }
    }
    
    public void Start()
    {
        SignalBus.Subscribe<CardSignals.OnCardUpgradedSignal>(OnCardUpdated);
        _ghostText.transform.parent.gameObject.SetActive(false);
        _ghostText.gameObject.SetActive(false);
        _skillPanelPip.SetActive(true);
        _upgradeAnimPlayer.gameObject.SetActive(false);
        FocusSkill(0f);
    }
    
    public void OnDestroy()
    {
        SignalBus.Unsubscribe<CardSignals.OnCardUpgradedSignal>(OnCardUpdated);
    }

    private void OnCardUpdated(CardSignals.OnCardUpgradedSignal obj)
    {
        if (obj.NewCardData.Id != _currentCardData.Id)
        {
            return;
        }

        ShowCardLevelUpPopup(obj.NewCardData);
    }
    
    protected override async UniTask AfterShow()
    {
        _animController.Show();
        await base.AfterShow();
    }

    private void SetupTopAnchor()
    {
        Vector2 offsetMax = _contentRect.offsetMax;
        offsetMax.y = _topBarHeightOffset;
        _contentRect.offsetMax = offsetMax;
    }

    private void SetupUI(CardData cardData, bool showButtons)
    {
        // get card game data from game data
        CardData oldCardData = _currentCardData;
        _currentCardData = cardData;
        bool animated = oldCardData != null && oldCardData.UILevel != _currentCardData.UILevel;

        if (cardData.IsOwned)
        {
            _locHelper.SetStatLevel(_level, _currentCardData.UILevel);
        }
        else if (cardData.ArenaUnlockIndex < 0)
        {
            var text = LocalizationManager.GetTermTranslation(LocConstants.CardWinEvent);
            _level.text = text;
        }
        else if (cardData.ArenaUnlockIndex > 0 && TrophyRoad.Current.GetCurrentArena().Data.Index >= _currentCardData.ArenaUnlockIndex)
        {
            var text = LocalizationManager.GetTermTranslation(LocConstants.CardOpenChests);
            _level.text = text;
        }
        else
        {
            var text = LocalizationManager.GetTermTranslation(LocConstants.CardUnlocksInArenaFull);
            text = text.Replace("{[ArenaIdx]}", $"{_currentCardData.ArenaUnlockIndex}");
            _level.text = text;
        }
        
        _level.gameObject.SetActive(true);
        
        float targetXpPercent = _currentCardData.CurrentLevelCount / (float)_currentCardData.RequiredCountToNextLevel;

        if (!animated)
        {   // set values directly
            _xpBar.value = targetXpPercent;
            _xpText.text = $"{_currentCardData.CurrentLevelCount}/{_currentCardData.RequiredCountToNextLevel}";
        }
        else
        {
            if (!StatsInFocus())
            {
                FocusStats(_animDuration);    
            }
            
            var xpBarTween = _xpBar.DOValue(targetXpPercent, _toNextLevelTweenTime)
                .SetEase(_toNextLevelTweenType)
                .SetDelay(StatsInFocus() ? 0f : _animDelayDuration);
            xpBarTween.startValue = 0; // set start value to 0 so it doesn't tween from current value
            
            _xpText.DoProgressCount
            (
                fromStartValue: oldCardData.CurrentLevelCount, 
                fromToValue : _currentCardData.CurrentLevelCount, 
                toStartValue: oldCardData.RequiredCountToNextLevel, 
                toToValue: _currentCardData.RequiredCountToNextLevel, 
                duration: _toNextLevelTweenTime,
                delay: StatsInFocus() ? 0f : _animDelayDuration,
                _toNextLevelTweenType
            );
        }

        
        var characterSettings = cardData.CharacterData;
        foreach (var statController in _stats)
        {
            var stat = characterSettings.GetStat(statController.StatType);
            var scalingSettings = QuantumUnityDB.Global.GetAsset(cardData.Data.ScalingSettings.Id) as CardScalingSettings;
            var multiplier = scalingSettings.StatMultipliers.First(s => s.Type == statController.StatType).Value;
            var currStatValue = CharacterCore.GetStatValueForLevel(multiplier, stat.Current, _currentCardData.UILevel).AsStatInt(statController.StatType);

            if (statController.StatType == StatTypes.Mana)
            {
                //mana needs to get value from stat.Max because stat.Current is set to 0
                currStatValue = CharacterCore.GetStatValueForLevel(multiplier, stat.Max, _currentCardData.UILevel).AsStatInt(statController.StatType);
            }
                
            if (_currentCardData.IsAtMaxLevel)
            {
                statController.Setup(currStatValue, 0, animated);
                statController.ApplyStyle(_baseStatStyle);
            }
            else
            {
                var nextStatValue = CharacterCore.GetStatValueForLevel(multiplier,
                    statController.StatType == StatTypes.Mana ? stat.Max : stat.Current, 
                    _currentCardData.UILevel).AsStatInt(statController.StatType);
                
                var statDifference = CharacterCore.GetStatValueForLevel(multiplier, 
                    stat.Current, 
                    _currentCardData.UILevel + 1).AsStatInt(statController.StatType) - nextStatValue;
                
                if (statController.StatType == StatTypes.Mana) //with mana, can't subtract the difference. Need whole value 
                {
                    statDifference = CharacterCore.GetStatValueForLevel(multiplier, 
                        statController.StatType == StatTypes.Mana ? stat.Max : stat.Current, 
                        _currentCardData.UILevel + 1).AsStatInt(statController.StatType);

                    if (nextStatValue - statDifference == 0) //if no difference by int (there should always be a difference by float)
                    {
                        statDifference = 0;
                    }
                }

                if (statDifference == 0)
                {
                    statController.Setup(currStatValue, 0, animated, StatsInFocus() ? 0f : _animDelayDuration);
                    statController.ApplyStyle(_baseStatStyle);
                }
                else
                {
                    statController.Setup(currStatValue, statDifference, animated, StatsInFocus() ? 0f : _animDelayDuration);
                    statController.ApplyStyle(_upgradedStatStyle);
                }
            }
        }

        foreach (var statShorthand in _statsShorthand)
        {
            var stat = characterSettings.GetStat(statShorthand.StatType);
            var scalingSettings = QuantumUnityDB.Global.GetAsset(cardData.Data.ScalingSettings.Id) as CardScalingSettings;
            var multiplier = scalingSettings.StatMultipliers.First(s => s.Type == statShorthand.StatType).Value;
            var currStatValue = CharacterCore.GetStatValueForLevel(multiplier, stat.Current, _currentCardData.UILevel).AsStatInt(statShorthand.StatType);

            if (statShorthand.StatType == StatTypes.Mana)
            {
                //mana needs to get value from stat.Max because stat.Current is set to 0
                currStatValue = CharacterCore.GetStatValueForLevel(multiplier, stat.Max, _currentCardData.UILevel).AsStatInt(statShorthand.StatType);
            }
            
            statShorthand.Setup(currStatValue);
        }

        // Only show buttons on owned cards
        SetButtons(cardData, showButtons);
        //set upgrade images
        _updradeStateEventInvoker.ExecuteEvent(PlayerData.Current.PlayerCards.CanUpgradeCard(_currentCardData.Id)
            ? "Upgrade"
            : "NoUpgrade");

        if (animated) // play upgrade animation if card level changed
        {
            DelayUpgradeAnim();
        } 
        else
        {
            _upgradeAnimPlayer.gameObject.SetActive(false);
        }
        
        if (cardData.Data == null || cardData.CharacterData == null)
        {   // log error and return. We can't do anything else without card game data
            DebugLog.LogError($"{GetType().Name}: Card [{_currentCardData.Id}] without data for type [{_currentCardData.Type}]", this);
            return;
        }
        
        foreach (var background in _backgroundColors)
        {
            background.SetActive(background.name.Contains(cardData.CharacterData.Color.ToString()));
        }
        
        _locHelper.SetTerm(_statsTitle, LocConstants.CardCharacterStatsTitle);
        _locHelper.SetTerm(_name, cardData.CharacterData.Name);
        _locHelper.SetTerm(_rarity, LocConstants.GetCardRarity(cardData.CharacterData.Rarity));
        _rarityStateEventInvoker.ExecuteEvent(cardData.CharacterData.Rarity.ToString());
        _locHelper.SetTerm(_skillName, cardData.CharacterData.AbilityTitle);
        _skillName.text = LocalizationManager.GetTermTranslation(LocConstants.GetGemColorKey(cardData.CharacterData.Color.ToString().ToLower())) + _skillName.text;
        _locHelper.SetTerm(_skillDescription, cardData.CharacterData.AbilityDescription);

        _characterImage.LoadAssetAsync(cardData.Data.InfoScreenHeaderImage.AssetGUID);
        _abilityImage.LoadAssetAsync(cardData.Data.InfoScreenAbilityImage.AssetGUID);
    }
    
    void SetButtons(CardData cardData, bool showButtons)
    {
        if (!showButtons)
        {
            _buttonParent.SetActive(false);
            return;
        }
            
        _upgradeButton.Setup(cardData, this);
        _buttonParent.SetActive(cardData.IsOwned);
            
        if (cardData.IsOwned)
        {
            _notOwnedButton.SetActive(false);
        
            if (_isCardInDeck)
            {   // prevent showing "use" button if we are not in home screen
                _useButtonParent.SetActive(false);
            }
            else
            {
                _useButtonParent.SetActive(true);
                _useButton.SetActive(true);
            }
        }
        else
        {
            _notOwnedButton.SetActive(true);
            _useButtonParent.SetActive(true);
            _useButton.SetActive(false);
        }
    }

    private async UniTask DelayUpgradeAnim()
    {
        await UniTask.WaitForSeconds(_animDelayDuration);
        _upgradeAnimPlayer.gameObject.SetActive(true);
        _upgradeAnimPlayer.Show();
    }

    void Update()
    {
        if (UnityEngine.Input.touchCount > 0)
        {
            Touch touch = UnityEngine.Input.GetTouch(0);

            switch (touch.phase)
            {
                case TouchPhase.Began:
                    _scrollTween?.Kill();
                    break;
                case TouchPhase.Moved:
                    _touchDelta = touch.deltaPosition;
                    break;
                case TouchPhase.Ended:
                    if (TouchUtils.CheckSwipeLeft(_touchDelta))
                    {
                        FocusStats(_animDuration);
                    }
                    else if (TouchUtils.CheckSwipeRight(_touchDelta))
                    {
                        FocusSkill(_animDuration);
                    }
                    else
                    {
                        CheckAndSnapPos();
                    }
                    break;
            }
        }
    }

    private void CheckAndSnapPos()
    {
        if (StatsInFocus())
        {
            FocusStats(_animDuration);
        }
        else
        {
            FocusSkill(_animDuration);
        }
    }

    private bool StatsInFocus()
    {
        return Math.Abs(_scrollRect.content.rect.position.x) > _scrollRect.content.rect.width / 2;
    }

    private void FocusSkill(float duration)
    {
        _scrollRect.StopMovement();
        _scrollTween.Kill();
        _scrollTween = _scrollRect.content.DOAnchorPosX(0f, duration);
        _skillPanelPip.SetActive(true);
        _statPanelPip.SetActive(false);
    }

    private void FocusStats(float duration)
    {
        _scrollRect.StopMovement();
        _scrollTween.Kill();
        _scrollTween = _scrollRect.content.DOAnchorPosX(-_scrollRect.content.sizeDelta.x, duration: duration);
        _skillPanelPip.SetActive(false);
        _statPanelPip.SetActive(true);
    }


    public void ActivateGhostText(string textToDisplay)
    {
        if (_ghostText.gameObject.activeInHierarchy)
        {
            return;
        }
        
        _ghostText.text = textToDisplay;
        _ghostText.gameObject.SetActive(true);
        _ghostText.transform.parent.gameObject.SetActive(true);
        _ghostTextDirector.Play();
    }
    
    public void OnUseClicked()
    {
        GoBack();
        SignalBus.Fire(new DeckbuildingSignals.OnCardUseSignal(_currentCardData));
    }

    protected override void OnWillShow()
    {
        base.OnWillShow();
        
        Canvas[] ownCanvases = GetComponentsInChildren<Canvas>(true);

        if (ownCanvases.Length == 0)
        {
            return;
        }
        
        UITopPanelHudCanvasController topPanelCanvas = IUIHomeScreenController.Instance.TopPanelCanvas;
        topPanelCanvas.ShowOverPopup(ownCanvases[0].sortingOrder + 2, GetComponent<Canvas>().worldCamera);
    }

    protected override async UniTask OnWillHide()
    {
        _animController.Hide();
        await UniTask.WaitWhile(()=>_animController.IsPlaying);
        await base.OnWillHide();
    }

    protected override void AfterHide()
    {
        base.AfterHide();
        UITopPanelHudCanvasController topPanelCanvas = IUIHomeScreenController.Instance.TopPanelCanvas;
        topPanelCanvas.UnshowOverPopup();
    }

    private void ShowCardLevelUpPopup(CardData data)
    {
        INavigationManager.Instance.ShowPopup<UILevelCardPopupController>(setupAction: popup =>
            {
                //close this selected card popup before level card popup is added to popup stack
                IUIManager.Instance.TryClose(this); 
                popup.Init(data, _currentCardData);
            });
    }
}
