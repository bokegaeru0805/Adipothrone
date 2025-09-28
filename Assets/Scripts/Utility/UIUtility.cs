using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class UIUtility
{
    /// <summary>
    /// 指定されたImageにSpriteを設定し、Spriteの縦横比を維持しつつ、
    /// 正方形のImage内で最大辺がちょうど収まるようにサイズ調整する。
    /// もし変更があったら、SayDialog.cs内の同じメソッドも更新してください。
    /// </summary>
    /// <param name="image">表示対象のUI Image（正方形）</param>
    /// <param name="sprite">表示するSprite</param>
    /// <param name="baseSize">正方形Imageの基準サイズ（例：128など）</param>
    public static void SetSpriteFitToSquare(Image image, Sprite sprite, float baseSize)
    {
        // nullチェック：どちらかが未設定ならログを出して終了
        if (image == null)
        {
            Debug.LogWarning("UIUtility.SetSpriteFitToSquare: Image is null.");
            return;
        }

        if (sprite == null)
        {
            if (image.gameObject.activeInHierarchy)
            {
                image.gameObject.SetActive(false);
            }
            return;
        }

        // ImageにSpriteを設定
        image.sprite = sprite;

        // アスペクト比を維持して描画
        image.preserveAspect = true;

        // Spriteの元のピクセルサイズを取得（RectはSpriteの切り抜き範囲）
        float width = sprite.rect.width;
        float height = sprite.rect.height;

        // 縦と横のうち、長い方を基準にしてスケーリング比を計算
        float maxSide = Mathf.Max(width, height);

        // スケール率（1.0を超えないように調整）
        float scaleX = width / maxSide;
        float scaleY = height / maxSide;

        // Imageのサイズ（sizeDelta）を、Spriteに合わせてスケーリング
        // 正方形ベースサイズを元に、縦横比を保ったサイズに変更
        image.rectTransform.sizeDelta = new Vector2(baseSize * scaleX, baseSize * scaleY);

        if (!image.gameObject.activeInHierarchy)
        {
            image.gameObject.SetActive(true); // Imageが非表示なら表示する
        }
    }

    /// <summary>
    /// 指定したアイテムリストの一部（ページ）を、対応するUIボタンに割り当てて表示し、
    /// 現在の選択ボタン（カーソル）位置をページ移動に応じて決定します。
    /// </summary>
    /// <param name="buttons">アイテム表示用のボタンリスト（例: 20個のボタン）</param>
    /// <param name="rowCount">UIの行数（例: 5行4列なら rowCount = 5）</param>
    /// <param name="items">表示対象のアイテムデータ（例: 全所持アイテム）</param>
    /// <param name="page">表示するページ番号（0から開始）</param>
    /// <param name="previousRow">前ページで選択されていたボタンの行（0〜rowCount-1）</param>
    /// <param name="moveRight">右方向にページを送ったかどうか（true = 次のページ）</param>
    /// <returns>ページが存在し割り当て成功した場合 true、範囲外なら false</returns>
    public static bool AssignItemsToButtons(
        List<Button> buttons,
        int rowCount,
        List<ItemEntry> items,
        int page,
        int previousRow,
        bool moveRight
    )
    {
        // 1ページあたりの表示可能アイテム数（ボタン数）
        int itemsPerPage = buttons.Count;

        // 今回のページで表示するアイテムの開始インデックス
        int startIndex = page * itemsPerPage;

        // 列数（1行に何個のボタンが並ぶか）を求める。例: 20個のボタン ÷ 5行 = 4列
        int columnCount = 0;

        // 選択対象となるボタンのインデックスを決めるための変数
        int selectIndex = 0;

        if (rowCount > 0)
        {
            // ボタン数と行数から、列数（横）を計算（端数を切り上げ）
            columnCount = Mathf.CeilToInt((float)buttons.Count / rowCount);
        }

        // 指定されたページが存在しない場合は、何もせず false を返す
        if (startIndex >= items.Count)
        {
            return false;
        }

        // このページで実際に表示される有効なアイテム数（= ボタン数）
        int validButtonCount = 0;

        for (int i = 0; i < buttons.Count; i++)
        {
            int itemIndex = startIndex + i;

            if (itemIndex < items.Count)
            {
                // ボタンが IItemAssignable を実装しているか確認（例: アイテムIDを持つUI部品）
                IItemAssignable itemButton = buttons[i].GetComponent<IItemAssignable>();
                if (itemButton != null)
                {
                    // アイテムIDをボタンに割り当て（表示や内部ID設定など）
                    itemButton.AssignItem(EnumIDUtility.FromID(items[itemIndex].itemID));
                    buttons[i].gameObject.SetActive(true);
                }
                else
                {
                    // インターフェースが未設定のボタンは非表示に（ミス防止）
                    Debug.LogWarning($"ボタン {i} に IItemAssignable がありません。");
                    buttons[i].gameObject.SetActive(false);
                    continue;
                }

                validButtonCount++;
            }
            else
            {
                // 該当アイテムがない分のボタンは非表示にする
                buttons[i].gameObject.SetActive(false);
            }
        }

        // --- 選択インデックスの決定処理 ---

        if (moveRight)
        {
            // ▶ 右にページを送ったとき：
            // - 前ページで選択していた行（previousRow）が、
            //   次ページでも存在している場合 → その行の先頭ボタンに合わせる
            // - 次ページに同じ行が存在しない（ボタンが不足している）場合 →
            //   最終行の先頭ボタンに合わせる（validButtonCount - 1 から算出）
            if (validButtonCount > previousRow * columnCount)
            {
                selectIndex = previousRow * columnCount;
            }
            else
            {
                selectIndex = (int)((validButtonCount - 1) / columnCount) * columnCount;
            }
        }
        else
        {
            // ◀ 左に戻ったとき：
            // - 前ページで選択していた行（previousRow）が、
            //   戻ったページにも存在する場合 → 同じ行の先頭に合わせる
            // - 存在しない（ボタン数が少ない）場合 →
            //   最後の有効なボタンを選択（末尾）
            if (validButtonCount >= (previousRow + 1) * columnCount)
            {
                selectIndex = (previousRow + 1) * columnCount - 1;
            }
            else
            {
                selectIndex = validButtonCount - 1;
            }
        }

        // インデックスがボタン範囲内に収まるように制限（バグ防止）
        selectIndex = Mathf.Clamp(selectIndex, 0, validButtonCount - 1);

        // 選択対象のボタンを選択状態にし、EventSystem に反映（キーボード/パッド操作用）
        EventSystem.current.SetSelectedGameObject(buttons[selectIndex].gameObject);

        return true;
    }

    public static bool AssignItemsVerticalNavigation(
        List<Button> buttons,
        List<ItemEntry> items,
        int page,
        bool moveDown
    )
    {
        // 1ページあたりの表示可能アイテム数（ボタン数）
        int itemsPerPage = buttons.Count;

        // 今回のページで表示するアイテムの開始インデックス
        int startIndex = page * itemsPerPage;

        // 選択対象となるボタンのインデックスを決めるための変数
        int selectIndex = 0;

        // 指定されたページが存在しない場合は、何もせず false を返す
        if (startIndex >= items.Count)
        {
            return false;
        }

        // このページで実際に表示される有効なアイテム数（= ボタン数）
        int validButtonCount = 0;

        for (int i = 0; i < buttons.Count; i++)
        {
            int itemIndex = startIndex + i;

            if (itemIndex < items.Count)
            {
                // ボタンが IItemAssignable を実装しているか確認（例: アイテムIDを持つUI部品）
                IItemAssignable itemButton = buttons[i].GetComponent<IItemAssignable>();
                if (itemButton != null)
                {
                    // アイテムIDをボタンに割り当て（表示や内部ID設定など）
                    itemButton.AssignItem(EnumIDUtility.FromID(items[itemIndex].itemID));
                    buttons[i].gameObject.SetActive(true);
                }
                else
                {
                    // インターフェースが未設定のボタンは非表示に（ミス防止）
                    Debug.LogWarning($"ボタン {i} に IItemAssignable がありません。");
                    buttons[i].gameObject.SetActive(false);
                    continue;
                }

                validButtonCount++;
            }
            else
            {
                // 該当アイテムがない分のボタンは非表示にする
                buttons[i].gameObject.SetActive(false);
            }
        }

        // --- 選択インデックスの決定処理 ---

        if (moveDown)
        {
            selectIndex = 0; // 下に移動した場合は、最初のボタンを選択
        }
        else
        {
            selectIndex = validButtonCount - 1; // 上に移動した場合は、最後のボタンを選択
        }

        // インデックスがボタン範囲内に収まるように制限（バグ防止）
        selectIndex = Mathf.Clamp(selectIndex, 0, validButtonCount - 1);

        // 選択対象のボタンを選択状態にし、EventSystem に反映（キーボード/パッド操作用）
        EventSystem.current.SetSelectedGameObject(buttons[selectIndex].gameObject);

        return true;
    }
}
