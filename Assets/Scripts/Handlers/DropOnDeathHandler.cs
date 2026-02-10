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

        // ループに入る前に、セーブデータのエントリーを1回だけ取得しておく
        // これでループごとの Find 検索(重い処理)を回避する
        var recordEntry = GameManager.instance.savedata.EnemyRecordData.GetOrCreateEntry(
            enemyData.enemyID
        );

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
        float luckEffectDelta = PlayerEffectManager.instance.GetDeltaValue(StatusEffectType.Luck);

        // 現在の討伐数を取得（CharacterHealth.HandleDeathFlowで加算済みと仮定）
        // Entryから直接値を取るので高速
        int currentKillCount = recordEntry.killCount;

        //プレイヤーレベルを取得（PlayerLevelManagerが存在すると仮定）
        int currentPlayerLevel =
            PlayerLevelManager.instance != null ? PlayerLevelManager.instance.playerLv : 1;

        // ノーダメージフラグを取得
        // ※現状のコードには判定ロジックがないため、仮の実装としています。
        // ※実際にはGameManagerやPlayerControllerから「今回の戦闘で被弾したか」を取得してください。
        bool isNoDamage = true; // 仮: 常に成功

        // 敵が持つすべてのドロップ候補アイテムについて処理
        foreach (var drop in enemyData.dropItems)
        {
            // アイテムIDを取得（Enum -> int変換）
            Enum tempDropID = BaseItemManager.instance.GetItemIDFromData(drop.baseItemData);
            if (tempDropID == null)
                continue;
            int itemIDInt = EnumIDUtility.ToID(tempDropID);

            // ドロップに条件がある場合、条件をチェックする
            if (drop.hasCondition && drop.conditionType != DropConditionType.None)
            {
                // Uniqueアイテムの入手済みチェック
                if (
                    drop.isUnique
                    && GameManager.instance.savedata.EnemyRecordData.IsUniqueItemObtained(itemIDInt)
                )
                {
                    continue; // 既に入手済みならスキップ
                }

                // 1. 既に条件が解禁されているかチェック
                // Entryのプロパティに直接アクセスする（検索処理が走らない）
                bool isUnlocked = recordEntry.UnlockedConditionItemIds.Contains(itemIDInt);

                // 2. まだ解禁されていない場合、条件判定を行う
                if (!isUnlocked)
                {
                    bool conditionMet = false;

                    switch (drop.conditionType)
                    {
                        case DropConditionType.KillCountOver:
                            // 指定回数「以上」の撃破で解禁
                            conditionMet = currentKillCount >= drop.conditionValue;
                            break;
                        case DropConditionType.PlayerLevelUnder:
                            // プレイヤーレベルが指定値「以下」で解禁
                            conditionMet = currentPlayerLevel <= drop.conditionValue;
                            break;
                        case DropConditionType.NoDamage:
                            // ノーダメージで解禁
                            conditionMet = isNoDamage;
                            break;
                    }

                    if (conditionMet)
                    {
                        // 条件達成！ セーブデータに記録して解禁する
                        // Entryに直接追加する
                        if (!recordEntry.UnlockedConditionItemIds.Contains(itemIDInt))
                        {
                            recordEntry.UnlockedConditionItemIds.Add(itemIDInt);
                        }
                        // ※即時ドロップさせたい場合はそのまま下へ進む
                    }
                    else
                    {
                        // 条件未達成かつ未解禁なので、このアイテムはドロップしない
                        continue;
                    }
                }
            }

            //実際のドロップ率への加算数値を計算
            float luckBonusRate = luckEffectDelta * drop.luckBonusMultiplier;

            if (drop.isUnique)
            {
                // 確率判定
                bool isDropped =
                    UnityEngine.Random.Range(0f, 100f) <= drop.dropChance + luckBonusRate;
                if (!isDropped)
                    continue; // 外れたら終了

                // 当選！ 即座に「入手済み」としてマーキング (同時ドロップ防止)
                GameManager.instance.savedata.EnemyRecordData.MarkUniqueItemAsObtained(itemIDInt);
            }

            for (int i = 0; i < drop.maxDropCount; i++)
            {
                // 通常アイテムの場合は個別に確率判定
                if (!drop.isUnique)
                {
                    bool isDropped =
                        UnityEngine.Random.Range(0f, 100f) <= drop.dropChance + luckBonusRate;
                    if (!isDropped)
                        continue;
                }
                // ドロップ位置を少しランダムにずらす（自然な演出のため）
                Vector2 offset = UnityEngine.Random.insideUnitCircle * ItemPositionOffsetRadius;
                Vector3 dropPos = dropBasePos + new Vector3(offset.x, offset.y, 0);

                // 【重要】DropItemはシーンオブジェクト（EnemyActivator）の子になるため、
                // シーン遷移時の整合性を保つために PersistentInstance ではなく SceneInstance を使用する。
                GameObject dropObj = ObjectPooler.SceneInstance.SpawnFromPool(
                    GameConstants.DROP_ITEM_POOLTAG,
                    dropPos,
                    Quaternion.identity
                );

                // 親オブジェクト指定がある場合は、生成後に設定する
                if (dropObj != null && parent != null)
                {
                    dropObj.transform.SetParent(parent);
                }

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
                    dropScript.ReturnToPool();
                    Debug.LogWarning("ドロップアイテムのIDが取得できませんでした");
                    continue;
                }

                // DropItemスクリプトにIDを設定
                dropScript.DropID = dropID;

                // ドロップしたアイテムを図鑑の「確認済みリスト」に登録する
                if (!recordEntry.UnlockedDropItemIds.Contains(itemIDInt))
                {
                    recordEntry.UnlockedDropItemIds.Add(itemIDInt);
                }

                if (drop.isUnique)
                {
                    // 即座に獲得して消滅させる（ロスト防止）
                    dropScript.AcquireInstantly();
                    break;
                }
                else
                {
                    // アイテムの種類ID（TypeID）を取得
                    int dropIDType = EnumIDUtility.ExtractTypeID(itemIDInt); // 変数再利用で少し効率化

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
                GameObject coinObj = ObjectPooler.SceneInstance.SpawnFromPool(
                    GameConstants.DROP_ITEM_POOLTAG,
                    dropPos,
                    Quaternion.identity
                );

                if (coinObj != null && parent != null)
                {
                    coinObj.transform.SetParent(parent);
                }

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
