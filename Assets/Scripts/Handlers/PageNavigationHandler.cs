using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// UIのページ切り替えを入力で制御するコンポーネント。
/// IPageNavigable を実装したクラスと連携し、
/// 選択中のボタンに応じて左右のページ移動を行う。
/// </summary>
public class PageNavigationHandler : MonoBehaviour
{
    private GameObject previousSelected; // 前フレームに選択されていたUIオブジェクト

    [SerializeField]
    private MonoBehaviour targetNavigable; // IPageNavigable を実装している MonoBehaviour をアタッチする

    private IPageNavigable navigable; // 実際のナビゲーション操作対象

    private void Awake()
    {
        // targetNavigable を IPageNavigable としてキャスト
        navigable = targetNavigable as IPageNavigable;
        if (navigable == null)
        {
            Debug.LogError("targetNavigable に IPageNavigable を実装したクラスを指定してください");
        }
    }

    private void Update()
    {
        // EventSystem や navigable が null の場合は処理しない
        if (EventSystem.current == null || navigable == null)
            return;

        // 現在選択されている UI 要素を取得
        GameObject selected = EventSystem.current.currentSelectedGameObject;
        if (selected == null)
            return;

        // 選択が前回と変わっていれば、状態を更新して以降の処理をスキップ
        if (selected != previousSelected)
        {
            previousSelected = selected;
            return;
        }

        // 現在選択されているオブジェクトが Button でなければ処理しない
        Button selectedButton = selected.GetComponent<Button>();
        if (selectedButton == null)
            return;

        // --- ページ移動処理 ---

        // 右端のボタンが選択されている状態で、右入力が押されたとき
        if (navigable.RightSideButtons.Contains(selectedButton))
        {
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
            {
                // 選択されているボタンの右端のボタンの中のインデックスを取得
                int selectedIndexInRight = navigable.RightSideButtons.IndexOf(selectedButton);

                // 次のページが存在し、表示に成功したらページ番号を進める
                if (navigable.TryAssignItemsToPage(navigable.Page + 1, selectedIndexInRight, true))
                {
                    // ページ番号を進める
                    navigable.Page++;
                }
            }
        }

        // 左端のボタンが選択されていて、左入力されたとき
        if (navigable.LeftSideButtons.Contains(selectedButton))
        {
            if (
                navigable.Page > 0 // 最初のページより前へは移動しない
                && (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
            )
            {
                // 選択されているボタンの左端のボタンの中のインデックスを取得
                int selectedIndexInLeft = navigable.LeftSideButtons.IndexOf(selectedButton);

                // 前のページの表示に成功したらページ番号を戻す
                if (navigable.TryAssignItemsToPage(navigable.Page - 1, selectedIndexInLeft, false))
                {
                    navigable.Page--;
                }
            }
        }
    }
}
