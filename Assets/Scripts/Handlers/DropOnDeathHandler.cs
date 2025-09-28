using System;
using UnityEngine;

public static class DropOnDeathHandler
{
    private const float ItemPositionOffsetRadius = 1.5f;
    private const float CoinPositionOffsetRadius = 0.5f;

    public static void Drop(IDroppable droppable)
    {
        EnemyData enemyData = droppable.GetEnemyData();
        // データが無効なら処理しない
        if (enemyData == null || GameManager.instance == null)
            return;

        // EnemyActivatorのTransformを取得
        Transform parent = droppable.GetDropParent();
        // ドロップ位置を取得
        Vector3 dropBasePos = droppable.GetDropPosition();

        // 金貨（1000,100,10）をドロップする処理
        if (enemyData.dropMoney > 0)
        {
            int amount = enemyData.dropMoney;
            int coin100 = amount / 100;
            amount %= 100;
            int coin10 = amount / 10;
            amount %= 10;
            int coin1 = amount / 1;

            // 金額に応じてコインをドロップ
            DropCoins(100, coin100);
            DropCoins(10, coin10);
            DropCoins(1, coin1);
        }
        // 幸運の効果を取得
        float luckEffectDelta = PlayerEffectManager.instance.luckEffectStates.deltaValue;

        // 敵が持つすべてのドロップ候補アイテムについて処理
        foreach (var drop in enemyData.dropItems)
        {
            //実際のドロップ率への加算数値を計算
            float luckBonusRate = luckEffectDelta * drop.luckBonusMultiplier;

            for (int i = 0; i < drop.maxDropCount; i++)
            {
                // ドロップ確率によって抽選
                bool isDropped =
                    UnityEngine.Random.Range(0f, 100f) <= drop.dropChance + luckBonusRate;
                if (!isDropped)
                    continue;

                // ドロップ位置を少しランダムにずらす（自然な演出のため）
                Vector2 offset = UnityEngine.Random.insideUnitCircle * ItemPositionOffsetRadius;
                Vector3 dropPos = dropBasePos + new Vector3(offset.x, offset.y, 0);

                // ドロップアイテム用のプレハブを生成
                GameObject dropObj =
                    parent != null
                        ? UnityEngine.Object.Instantiate(
                            GameManager.instance.DropItemPrefab,
                            dropPos,
                            Quaternion.identity,
                            parent
                        )
                        : UnityEngine.Object.Instantiate(
                            GameManager.instance.DropItemPrefab,
                            dropPos,
                            Quaternion.identity
                        );

                // DropItemスクリプトを取得（存在しない場合は警告を出してスキップ）
                var dropScript = dropObj.GetComponent<DropItem>();
                if (dropScript == null)
                {
                    Debug.LogWarning("DropItem スクリプトがプレハブに存在しません");
                    continue;
                }

                // アイテムID（Enum）をデータから取得
                Enum dropID = BaseItemManager.instance.GetItemIDFromData(drop.baseItemData);
                if (dropID == null)
                {
                    // IDが取得できない場合はドロップを中止
                    UnityEngine.Object.Destroy(dropObj);
                    Debug.LogWarning("ドロップアイテムのIDが取得できませんでした");
                    continue;
                }

                // DropItemスクリプトにIDを設定
                dropScript.DropID = dropID;

                // アイテムの種類ID（TypeID）を取得
                int dropIDType = EnumIDUtility.ExtractTypeID(EnumIDUtility.ToID(dropID));

                // 装備アイテムなら宝箱スプライトを表示（通常アイテムとは区別）
                if ((int)TypeID.Blade <= dropIDType && dropIDType < (int)TypeID.Jewelry3)
                {
                    dropScript.SetTreasureSprite();
                }
                else
                {
                    // 通常アイテムのスプライトを設定
                    dropScript.SetDropItemSprite();
                }
            }
        }

        //経験値をドロップ
        if (enemyData.rewardExp > 0)
        {
            PlayerLevelManager.instance.AddExperience(enemyData.rewardExp);
        }

        // 各コイン種別に応じてプレハブを出現させる
        void DropCoins(int coinValue, int count)
        {
            for (int i = 0; i < count; i++)
            {
                // ドロップ位置を少しランダムにずらす
                Vector2 offset = UnityEngine.Random.insideUnitCircle * CoinPositionOffsetRadius;
                Vector3 dropPos = dropBasePos + new Vector3(offset.x, offset.y, 0);

                // 親オブジェクト(EnemyActivator)が存在する場合は、親の子としてドロップアイテムを生成し、
                // 存在しない場合はルートに生成する（親子関係を持たせない）
                GameObject coinObj =
                    parent != null
                        ? UnityEngine.Object.Instantiate(
                            GameManager.instance.DropItemPrefab,
                            dropPos,
                            Quaternion.identity,
                            parent
                        )
                        : UnityEngine.Object.Instantiate(
                            GameManager.instance.DropItemPrefab,
                            dropPos,
                            Quaternion.identity
                        );

                var dropScript = coinObj.GetComponent<DropItem>();
                if (dropScript == null)
                {
                    Debug.LogWarning("DropItemスクリプトがプレハブに見つかりません");
                    continue;
                }

                dropScript.DropMoney = coinValue;
                dropScript.SetMoneySprite();
            }
        }
    }
}
