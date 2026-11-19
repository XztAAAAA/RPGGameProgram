using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public sealed class UpgradePanelController : MonoBehaviour
{
    private const string IconResourcesPath = "UI/Upgrade/";
    private static readonly string[] CardNames =
    {
        "Item_1",
        "Item_2",
        "Item_3"
    };

    private readonly Button[] buttons = new Button[CardNames.Length];
    private readonly UnityAction[] clickHandlers =
        new UnityAction[CardNames.Length];
    private readonly Image[] icons = new Image[CardNames.Length];
    private readonly TMP_Text[] descriptions =
        new TMP_Text[CardNames.Length];

    private IReadOnlyList<cfg.Upgrade> displayedOffers;
    private Func<cfg.Upgrade, bool> selectionHandler;
    private bool selectionInProgress;
    private bool cardsInitialized;

    public bool ShowOffers(
        IReadOnlyList<cfg.Upgrade> offers,
        Func<cfg.Upgrade, bool> onSelected)
    {
        if (offers == null || offers.Count == 0 || onSelected == null)
        {
            Debug.LogError("强化技能面板：候选列表或选择回调无效。", this);
            return false;
        }

        if (!cardsInitialized && !InitializeCards())
            return false;

        displayedOffers = offers;
        selectionHandler = onSelected;
        selectionInProgress = false;

        for (int i = 0; i < CardNames.Length; i++)
        {
            bool hasOffer = i < offers.Count;
            Transform card = transform.Find(CardNames[i]);
            card.gameObject.SetActive(hasOffer);
            if (!hasOffer)
                continue;

            cfg.Upgrade upgrade = offers[i];
            string spriteKey = upgrade.IconKey;
            if (spriteKey.StartsWith("upgrade_", StringComparison.Ordinal))
                spriteKey = spriteKey.Substring("upgrade_".Length);

            Sprite icon = Resources.Load<Sprite>(
                IconResourcesPath + spriteKey
            );
            if (icon == null)
            {
                Debug.LogError(
                    $"强化技能面板：找不到图标 Resources/{IconResourcesPath}{spriteKey}。",
                    this
                );
                return false;
            }

            icons[i].sprite = icon;
            icons[i].preserveAspect = true;
            int nextLevel = PlayerUpgradeSystem.GetUpgradeLevel(upgrade.Id) + 1;
            descriptions[i].text =
                $"{upgrade.Name}\n{upgrade.Description}\n" +
                $"{GetRarityName(upgrade.Rarity)} · Lv.{nextLevel}";
        }

        gameObject.SetActive(true);
        return true;
    }

    private bool InitializeCards()
    {
        for (int i = 0; i < CardNames.Length; i++)
        {
            Transform card = transform.Find(CardNames[i]);
            if (card == null)
            {
                Debug.LogError(
                    $"强化技能面板：Prefab 中缺少 {CardNames[i]}。",
                    this
                );
                return false;
            }

            Transform iconTransform = card.Find("Image");
            Transform descriptionTransform = card.Find("desc");
            Image cardImage = card.GetComponent<Image>();
            icons[i] = iconTransform != null
                ? iconTransform.GetComponent<Image>()
                : null;
            descriptions[i] = descriptionTransform != null
                ? descriptionTransform.GetComponent<TMP_Text>()
                : null;

            if (cardImage == null || icons[i] == null ||
                descriptions[i] == null)
            {
                Debug.LogError(
                    $"强化技能面板：{CardNames[i]} 缺少卡片 Image、图标 Image 或描述文本。",
                    card
                );
                return false;
            }

            buttons[i] = card.GetComponent<Button>();
            if (buttons[i] == null)
                buttons[i] = card.gameObject.AddComponent<Button>();

            buttons[i].targetGraphic = cardImage;
            int cardIndex = i;
            clickHandlers[i] = () => SelectOffer(cardIndex);
            buttons[i].onClick.RemoveListener(clickHandlers[i]);
            buttons[i].onClick.AddListener(clickHandlers[i]);
        }

        cardsInitialized = true;
        return true;
    }

    private void SelectOffer(int index)
    {
        if (selectionInProgress || displayedOffers == null ||
            index < 0 || index >= displayedOffers.Count)
        {
            return;
        }

        selectionInProgress = true;
        if (!selectionHandler(displayedOffers[index]))
        {
            selectionInProgress = false;
            return;
        }

        gameObject.SetActive(false);
    }

    private static string GetRarityName(int rarity)
    {
        switch (rarity)
        {
            case 1:
                return "普通";
            case 2:
                return "稀有";
            case 3:
                return "史诗";
            default:
                return $"稀有度 {rarity}";
        }
    }
}
