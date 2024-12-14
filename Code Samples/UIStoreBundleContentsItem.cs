using Cysharp.Threading.Tasks;
using I2.Loc;
using Redemption.Scripts.Configuration;
using RedemptionSDK.AssetManager;
using RedemptionSDK.Core.Player;
using TMPro;
using UnityEngine;

public class UIStoreBundleContentsItem : MonoBehaviour
{
    [SerializeField] private UIStoreCard _card;
    [SerializeField] private ImageLoader  _imageLoader;
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private TextMeshProUGUI _descriptionText;
    
    public void Setup(ServerRewardItem item, bool includeDescription = false)
    {
        var rewardGameData = GameDataManager.Instance.ItemsDB.GetItem(item.Reward);

        switch (rewardGameData.ItemType)
        {
            case ItemsGameDataDB.ItemType.Chest:
                SetupFromChest(item, rewardGameData, includeDescription);
                break;
            case ItemsGameDataDB.ItemType.CardFragment or ItemsGameDataDB.ItemType.Card:
                SetupFromCardOrFragment(item, rewardGameData, includeDescription);
                break;
            default:
                SetupDefault(item, rewardGameData, includeDescription);
                break;
        }
    }

    private void SetupFromChest(ServerRewardItem item, ItemsGameDataDB.ItemEntry rewardGameData, bool includeDescription)
    {
        _imageLoader.gameObject.SetActive(true);
        _card.gameObject.SetActive(false);
            
        _imageLoader.TrySetSprite(rewardGameData.RewardIconAssetId, $"Image not found for {rewardGameData.RewardIconAssetId}").Forget();
        int ownArenaIndex = TrophyRoad.Current.GetCurrentArena().Data.Index;
        var chestSlotData = new ChestSlot(item.Reward, ownArenaIndex);
        chestSlotData.TryPostProcess(GameDataManager.Instance);
        _nameText.text = LocalizationManager.GetTranslation(chestSlotData.ChestData.NameLocKey);
        
        _nameText.text += " x" + item.Amount;

        if (includeDescription)
        {
            _descriptionText.text = LocalizationManager.GetTranslation(LocConstants.ChestDefaultDescription);
        }
    }

    private void SetupFromCardOrFragment(ServerRewardItem item, ItemsGameDataDB.ItemEntry rewardGameData, bool includeDescription)
    {
        _imageLoader.gameObject.SetActive(false);
        _card.gameObject.SetActive(true);
            
        _card.DisplayXp(false);
        _card.SetCard(PlayerData.Current.PlayerCards.GetByType(rewardGameData.CardId));
        _nameText.text = LocalizationManager.GetTranslation(rewardGameData.NameLocKey);
        
        _nameText.text += " x" + item.Amount;

        if (includeDescription)
        {
            _descriptionText.text = LocalizationManager.GetTranslation(rewardGameData.DescriptionLocKey);    
        }
    }

    private void SetupDefault(ServerRewardItem item, ItemsGameDataDB.ItemEntry rewardGameData, bool includeDescription)
    {
        _imageLoader.gameObject.SetActive(true);
        _card.gameObject.SetActive(false);
            
        _imageLoader.TrySetSprite(rewardGameData.RewardIconAssetId, $"Image not found for {rewardGameData.RewardIconAssetId}").Forget();
        _nameText.text = LocalizationManager.GetTranslation(rewardGameData.NameLocKey);
        
        _nameText.text += " x" + item.Amount;

        if (includeDescription)
        {
            _descriptionText.text = LocalizationManager.GetTranslation(rewardGameData.DescriptionLocKey);    
        }
    }
}