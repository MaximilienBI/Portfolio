using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using I2.Loc;
using Internal.UITools.StateEventInvoker;
using RedemptionGames.Constants;
using RedemptionGames.Transactions.Responses;
using RedemptionGames.Utilities;
using RedemptionSDK.AssetManager;
using RedemptionSDK.Core.Audio;
using RedemptionSDK.Core.Player;
using RedemptionSDK.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UITreasureChestRewardsSingleReward : MonoBehaviour
{
    private enum CardFragmentState
    {
        Normal = 0,
        Upgrade = 1
    }
    
    [SerializeField] private UITreasureChestRewardEntry _rewardController;
    [SerializeField] private StateEventInvoker _stateInvoker;

    [Header("Inventory Item")]
    [SerializeField] private TextMeshProUGUI _itemNameText;
    [SerializeField] private ImageLoader _itemAmountIcon;
    [SerializeField] private TextMeshProUGUI _itemAmountText;

    [Header("Card Fragment")]
    [SerializeField] private TextMeshProUGUI _cardNameText;
    [SerializeField] private TextMeshProUGUI _cardRarityText;
    [SerializeField] private TextMeshProUGUI _cardLevelText;
    [SerializeField] private TextMeshProUGUI _cardLevelProgressText; // current/max
    [SerializeField] private Slider _cardLevelProgressSlider;
    [SerializeField] private StateEventInvoker _cardFragmentStateInvoker;
    [SerializeField] private StateEventInvoker _cardBackStateInvoker;
    [SerializeField] private RectTransform _cardUpgradeArrowRect;
    [SerializeField] private CanvasGroup _newBannerCanvasGroup;
    [SerializeField] private TextMeshProUGUI _rarity;
    [SerializeField] private StateEventInvoker _rarityBannerStateEventInvoker;
    [SerializeField] private CanvasGroup _rarityBannerCanvasGroup;
    
    [Header("Animation")]
    [SerializeField] private float _countUpdateDuration = 0.5f;
    [SerializeField] private float _countUpdateDelay = 0.25f;
    [SerializeField] private Ease _countUpdateEaseType = Ease.OutQuad;
    
    [Header("Audio")]
    [SerializeField] private AudioClipReference UI_Chest_Counter_Start;
    [SerializeField] private AudioClipReference UI_Chest_Counter_Loop;
    private bool _skipAudio;
    private bool _generalReward;

    private readonly I2CompoundHelper _locHelper = new I2CompoundHelper();

    private Dictionary<string, int> _inventory = new Dictionary<string, int>();

    public void Setup(Reward reward, bool skipAudio = false, bool generalReward = false, 
        bool controlNewBannerShow = true)
    {
        _rewardController.Setup(reward, controlNewBannerShow);
        var itemsDb = GameDataManager.Instance.ItemsDB;
        var itemData = itemsDb.GetItem(reward.Id);
        
        _skipAudio = skipAudio;
        _generalReward = generalReward;
        
        switch (itemData.ItemType)
        {
            case ItemsGameDataDB.ItemType.CardFragment:
                ConfigureCardFragment(itemData, reward.Amount);
                break;
            default:
                ConfigureInventoryItem(itemData, reward.Amount);
                break;
        }
    }

    public void TurnOffBanners()
    {
        _rarityBannerCanvasGroup.DOKill();
        _newBannerCanvasGroup.DOKill();
        _rarityBannerCanvasGroup.gameObject.SetActive(false);
        _newBannerCanvasGroup.gameObject.SetActive(false);
    }
    
    private void ConfigureInventoryItem(ItemsGameDataDB.ItemEntry itemData, int amount)
    {
        // Set state based on non-card item type
        switch (itemData.ItemType)
        {
            case (ItemsGameDataDB.ItemType.Boost):
                _stateInvoker.ExecuteEvent($"{ItemsGameDataDB.ItemType.Boost}");
                break;
            default:
                _stateInvoker.ExecuteEvent($"{ItemsGameDataDB.ItemType.Currency}");
                break;
        }
        
        _locHelper.SetTerm(_itemNameText, itemData.NameLocKey);
        
        if (!_inventory.ContainsKey(itemData.Id))
        {
            _inventory[itemData.Id] = Inventory.Current.GetInventoryCount(itemData.Id);
        }

        var itemCount = _inventory[itemData.Id];
        int previousCount = itemCount-amount;
        _itemAmountText.text = $"<size=75%>x</size>{previousCount}";
        var textTween = _itemAmountText.DoTextCount
        (
            startValue: previousCount,
            endValue: itemCount,
            prefix: "x",
            duration: _countUpdateDuration,
            easeType: _countUpdateEaseType
        ).SetDelay(_countUpdateDelay);
        
        if (!_skipAudio)
        {
            IAudioManager.Instance.PlayAudio(_generalReward ? AudioConstants.UI_Receive_Reward : AudioConstants.UI_Chest_Open_Default);
        }
        
        textTween.OnStart(() =>
        {
            if (!_skipAudio)
            {
                IAudioManager.Instance.PlayAudio(UI_Chest_Counter_Start);
                IAudioManager.Instance.PlayAudio(UI_Chest_Counter_Loop);
            }
        });

        textTween.OnComplete(() =>
        {
            if (!_skipAudio)
            {
                IAudioManager.Instance.StopAudio(UI_Chest_Counter_Loop);
            }
        });
        
        TurnOffBanners();
        
        _itemAmountIcon.LoadAssetAsync(itemData.IconAssetId).Forget();
        _cardBackStateInvoker.ExecuteEvent(Quantum.CardRarity.Common.ToString());
        // log inventory increment
        DebugLog.Log($"{GetType().Name}: Inventory increment: {itemData.Id}: From {previousCount} to {itemCount}");
    }

    private void ConfigureCardFragment(ItemsGameDataDB.ItemEntry itemData, int amount)
    {
        _stateInvoker.ExecuteEvent(ItemsGameDataDB.ItemType.CardFragment.ToString());

        if (PlayerData.Current?.PlayerCards == null)
        {
            DebugLog.LogError($"{GetType().Name}:Unable to configure cardFragment: {nameof(PlayerData.Current)} or {nameof(PlayerData.Current.PlayerCards)} is null.");
            return;
        }
        
        var cardData = PlayerData.Current.PlayerCards.GetByType(itemData.CardId);
        var cardGameData = cardData.CharacterData;
        
        _locHelper.SetTerm(_cardNameText, cardGameData.Name);
        _locHelper.SetTerm(_cardRarityText, LocConstants.GetCardRarity(cardGameData.Rarity));
        _locHelper.SetStatLevel(_cardLevelText, cardData.UILevel);
        
        if (!_inventory.ContainsKey(cardData.Id))
        {
            _inventory[cardData.Id] = cardData.CurrentLevelCount;
        }
        
        int previousCount = cardData.CurrentLevelCount - amount;
        var inventoryCount = _inventory[cardData.Id];
        var toNextLevelCount = cardData.RequiredCountToNextLevel;
        _cardLevelProgressText.DOKill(); // kill any existing tween before starting a new one
        _cardLevelProgressText.text = $"{previousCount}/{toNextLevelCount}";
        var textTween = _cardLevelProgressText.DoTextCount
        (
            startValue: previousCount, 
            endValue: inventoryCount,
            suffix:$"/{toNextLevelCount}"
        ).SetDelay(_countUpdateDelay);
        
        if (!_skipAudio)
        {
            IAudioManager.Instance.PlayAudio(AudioConstants.GetRarityAudio(cardData.Data.Rarity.ToString()));
        }
        
        textTween.OnStart(() =>
        {
            if (!_skipAudio)
            {
                IAudioManager.Instance.PlayAudio(UI_Chest_Counter_Start);
                IAudioManager.Instance.PlayAudio(UI_Chest_Counter_Loop);
            }
        });
        
        textTween.OnComplete(() =>
        {
            if (!_skipAudio)
            {
                IAudioManager.Instance.StopAudio(UI_Chest_Counter_Loop);
            }
        });
        
        _cardLevelProgressSlider.DOKill(); // kill any existing tween before starting a new one
        _cardLevelProgressSlider.minValue = 0;
        _cardLevelProgressSlider.maxValue = toNextLevelCount;
        _cardLevelProgressSlider.value = previousCount;
        _cardLevelProgressSlider.DOValue(inventoryCount, _countUpdateDuration).SetDelay(_countUpdateDelay);

        _newBannerCanvasGroup.DOKill();
        if(!LocalStorage.Get(RGConstants.NewCardString(cardData.CharacterData.InternalName), false))
        {
            _newBannerCanvasGroup.alpha = 0f;
            _newBannerCanvasGroup.DOFade(1f, _countUpdateDuration).SetDelay(0.8333333f).OnStart(() =>
            {
                _newBannerCanvasGroup.gameObject.SetActive(true);
            }).onKill = () =>
            {
                _newBannerCanvasGroup.gameObject.SetActive(false);
            }; 
        }

        _rarityBannerCanvasGroup.DOKill();
        _rarityBannerCanvasGroup.alpha = 0f;
        _rarityBannerCanvasGroup.DOFade(1f, _countUpdateDuration).SetDelay(0.8333333f).OnStart(() =>
        {
            _rarityBannerCanvasGroup.gameObject.SetActive(true);
        }).onKill = () =>
        {
            _rarityBannerCanvasGroup.gameObject.SetActive(false);
        };
        
        _rarityBannerStateEventInvoker.ExecuteEvent(cardData.CharacterData.Rarity.ToString());
        _rarity.text = LocalizationManager.GetTranslation(LocConstants.GetCardRarity(cardData.CharacterData.Rarity));
        

        int loopCount = Mathf.Min(amount, 4);
        _cardUpgradeArrowRect.DOPunchScale(new Vector3(.25f, .25f), .15f).SetLoops(loopCount).SetDelay(_countUpdateDelay);
        
        string cardFragmentState = inventoryCount < toNextLevelCount ? CardFragmentState.Normal.ToString() : CardFragmentState.Upgrade.ToString();
        _cardFragmentStateInvoker.ExecuteEvent(cardFragmentState);
        _cardBackStateInvoker.ExecuteEvent(cardData.CharacterData.Rarity.ToString());
        // log card fragment increment
        DebugLog.Log($"{GetType().Name}: Card fragment increment: {cardData.CharacterData.Name}: From {previousCount} to {inventoryCount}");
    }
}
