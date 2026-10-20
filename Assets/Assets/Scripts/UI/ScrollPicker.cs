using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.UI.Extensions;

public class ScrollPicker : MonoBehaviour
{
    [Header("Panel")]
    public GameObject pickerOverlay;
    public TMP_Text titleText;
    public Button confirmButton;
    public Button cancelButton;

    [Header("Left Column")]
    public UIVerticalScroller leftScroller;
    public RectTransform leftContent;

    [Header("Right Column")]
    public UIVerticalScroller rightScroller;
    public RectTransform rightContent;

    [Header("Prefab")]
    public GameObject pickerItemPrefab;

    [Header("Layout")]
    public float itemHeight = 80f;

    private string[] leftOptions;
    private string[] rightOptions;
    private int leftSelected;
    private int rightSelected;
    private int lastLeftFocused = -1;
    private int lastRightFocused = -1;
    private Action<int, int> onConfirmCallback;
    private Action onCancelCallback;
    private bool isOpen;

    void Start()
    {
        if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirmClicked);
        if (cancelButton != null) cancelButton.onClick.AddListener(OnCancelClicked);
        if (pickerOverlay != null) pickerOverlay.SetActive(false);
    }

    void Update()
    {
        if (!isOpen) return;

        if (leftScroller != null)
        {
            int fi = leftScroller.FocusedElementIndex;
            if (fi >= 0 && fi != lastLeftFocused)
            {
                lastLeftFocused = fi;
                leftSelected = fi;
            }
        }
        if (rightScroller != null)
        {
            int fi = rightScroller.FocusedElementIndex;
            if (fi >= 0 && fi != lastRightFocused)
            {
                lastRightFocused = fi;
                rightSelected = fi;
            }
        }
    }

    public void Show(string title, string[] left, string[] right, int leftInitial, int rightInitial,
                     Action<int, int> onConfirm, Action onCancel = null, bool hideButtons = false)
    {
        leftOptions = left;
        rightOptions = right;
        onConfirmCallback = onConfirm;
        onCancelCallback = onCancel;
        leftSelected = Mathf.Clamp(leftInitial, 0, left.Length - 1);
        rightSelected = Mathf.Clamp(rightInitial, 0, right.Length - 1);
        lastLeftFocused = -1;
        lastRightFocused = -1;

        // Detach from old scroller items before we destroy/rebuild them.
        if (leftScroller != null) leftScroller.ArrayOfElements = new GameObject[0];
        if (rightScroller != null) rightScroller.ArrayOfElements = new GameObject[0];

        if (titleText != null) titleText.text = title;
        if (confirmButton != null) confirmButton.gameObject.SetActive(!hideButtons);
        if (cancelButton != null) cancelButton.gameObject.SetActive(!hideButtons);
        if (pickerOverlay != null) pickerOverlay.SetActive(true);
        isOpen = true;

        StartCoroutine(BuildNextFrame(leftSelected, rightSelected));
    }

    public void Hide()
    {
        isOpen = false;
        if (pickerOverlay != null) pickerOverlay.SetActive(false);

        if (leftScroller != null) leftScroller.ArrayOfElements = new GameObject[0];
        if (rightScroller != null) rightScroller.ArrayOfElements = new GameObject[0];
    }

    public bool IsOpen() => isOpen;

    public void GetSelection(out int left, out int right)
    {
        left = leftSelected;
        right = rightSelected;
    }

    IEnumerator BuildNextFrame(int lSnap, int rSnap)
    {
        yield return null;

        if (pickerItemPrefab == null)
        {
            Debug.LogError("[ScrollPicker] Prefab missing.");
            yield break;
        }

        ClearChildren(leftContent);
        ClearChildren(rightContent);
        yield return null;

        var leftItems = BuildColumn(leftContent, leftOptions);
        var rightItems = BuildColumn(rightContent, rightOptions);

        SetContentHeight(leftContent, leftOptions.Length);
        SetContentHeight(rightContent, rightOptions.Length);

        if (leftScroller != null) leftScroller.UpdateChildren(lSnap, leftItems);
        if (rightScroller != null) rightScroller.UpdateChildren(rSnap, rightItems);

        WireClicks(leftItems, leftScroller, true);
        WireClicks(rightItems, rightScroller, false);

        lastLeftFocused = lSnap;
        lastRightFocused = rSnap;
        leftSelected = lSnap;
        rightSelected = rSnap;
    }

    void ClearChildren(RectTransform content)
    {
        if (content == null) return;
        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);
    }

    GameObject[] BuildColumn(RectTransform content, string[] options)
    {
        if (content == null || options == null) return new GameObject[0];
        var items = new GameObject[options.Length];
        for (int i = 0; i < options.Length; i++)
        {
            var go = Instantiate(pickerItemPrefab, content);
            items[i] = go;
            var txt = go.GetComponentInChildren<TMP_Text>();
            if (txt != null) txt.text = options[i];
        }
        return items;
    }

    void SetContentHeight(RectTransform content, int count)
    {
        if (content == null) return;
        float h = count * itemHeight * 2f + 1000f;
        content.sizeDelta = new Vector2(content.sizeDelta.x, h);
    }

    void WireClicks(GameObject[] items, UIVerticalScroller scroller, bool isLeft)
    {
        for (int i = 0; i < items.Length; i++)
        {
            int idx = i;
            var btn = items[i].GetComponent<Button>();
            if (btn == null) continue;
            btn.onClick.AddListener(() => OnItemTapped(scroller, idx, isLeft));
        }
    }

    void OnItemTapped(UIVerticalScroller scroller, int index, bool isLeft)
    {
        if (scroller == null) return;
        scroller.SnapToElement(index);
        if (isLeft) { leftSelected = index; lastLeftFocused = index; }
        else { rightSelected = index; lastRightFocused = index; }
    }

    void OnConfirmClicked()
    {
        var cb = onConfirmCallback;
        int l = leftSelected;
        int r = rightSelected;
        Hide();
        cb?.Invoke(l, r);
    }

    void OnCancelClicked()
    {
        var cb = onCancelCallback;
        Hide();
        cb?.Invoke();
    }
}