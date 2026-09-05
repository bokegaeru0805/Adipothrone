# 店内会話の設定と移行

## 役割

- NPCDialogueTrigger: 調べたときのフラグ条件から、通常会話かショップ起動かを選ぶ。
- ShopInteractionTrigger: ShopDataと、同じGameObjectのShopConversationをShopUIManagerへ渡す。
- ShopData: 品揃え・開始/終了の挨拶。
- ShopConversation: 店内の「会話」で使うFlowchart・ブロック・フラグ条件。
- ShopConversationMenuCommand: 実行可能な店内会話があるときだけメニュー項目を追加。
- StartShopConversationCommand: 終了挨拶なしでショップを閉じ、店内会話を開始する。

店舗IDから会話内容を選ぶ処理、ShopUIManager上のconversationHandlerObject設定は廃止。
IShopConversationはIsAvailableとTryStartConversation()へ変更。
ShopConversation.StartShopConversation(ShopName)は廃止。
ShopUIManager.StartShopConversation()とOpenShop(ShopData)は残しているが、後者だけでは店内会話は設定されない。

## NPCへの設定

1. ShopInteractionTriggerと同じGameObjectにShopConversationを追加する。
2. Target Flowchartに、そのNPCの店内会話ブロックを持つFlowchartを指定する。
3. Default Block Nameに、どの条件にも一致しない場合のブロック名を設定する。
4. Conversation Conditionsに必要な条件を追加する。
   - Required Flagsはすべて一致（AND）。
   - 条件リストは下から評価し、最初に一致したブロックを使用する。
   - 条件にはショップ起動や追加イベントを持たせない。会話中のイベントはFungus側に設定する。
5. ShopInteractionTriggerのShop Dataも設定する。

デフォルト名を空欄にすると、条件に一致しない間は会話の選択肢を表示しない。
選ばれたブロックが存在しない場合、ShopConversationが無効な場合、またはコンポーネント自体がない場合も表示しない。
設定不備で表示されない場合はTarget Flowchart・ブロック名・条件を確認する。
NPCDialogueTriggerがどの動作で店を開いた場合でも、店内会話にはこのShopConversationを使う。

## 共通Flowchartの設定（Unity Editorで実施）

Assets/Prefabs/Managers/GlobalFlowchart.prefabの保存内容を確認した範囲では、
StartShopDialogue内の「会話」はDoShopTalk条件のIf/Endで囲まれている。

1. StartShopDialogueの「会話」用Menuを、Shop > Shop Conversation Menuへ置き換える。
2. Textを「会話」、Target Blockを既存のStartShopConversationブロックに設定する。
   カスタムMenu Dialogなどを使用している場合は旧Menuの設定を引き継ぐ。
3. 会話の表示は新コマンドが判定するため、この項目だけを囲む旧DoShopTalkのIf/Endを外す。
   他のコマンドを囲んでいないことをEditorで確認してから操作する。変数自体の削除は不要。
4. StartShopConversationブロック内のStart Shop Conversationコマンドを使用する。
   既存のスクリプトGUIDは維持しているため、このコマンドの置き換えは不要。
5. 共通Prefabに対するScene側のoverrideがあれば、実際に使用されるFlowchartにも設定を反映する。

Start Shop Conversationは、店内会話ブロックの開始に成功した時点でショップUIを閉じ、
ショップの現在状態とイベント購読を解除して、Fungus側の次のコマンドへ進む。
終了挨拶は再生せず、店内会話のTalkEnd後はフィールド操作へ戻る。
店内会話ブロックからショップを開き直す処理は追加しない。

旧Manager側のShopConversationコンポーネントをNPCへ移す際は、Target Flowchartの設定を確認する。
古いコンポーネントの削除は、他の参照がないことをUnity Editorで確認してから行う。

## Village_ShopGirl_Objectの旧コードと同じ会話選択

Target Flowchart: 次のブロックを持つChapter1SceneのLocalFlowchart。
Default Block Name: Village_ShopGirl_Default

Conversation Conditionsを次の順（上から下）に登録する。
各条件はChapter1TriggeredEventのBoolフラグがtrue。

| リスト順 | フラグ | Block Name To Execute |
| --- | --- | --- |
| 0 | WellQuestComplete | Village_ShopGirl_CompletedWellQuest |
| 1 | UpperRiverReached | Village_ShopGirl_ArrivedUpstream |
| 2 | HeardRumorAboutShopGirl | Village_ShopGirl_RiverQuestCompleted |

下の条件ほど優先されるため、複数のフラグがtrueでも旧コードの優先順位と一致する。
NPCDialogueTrigger側のクエスト会話条件は別の設定であり、変更不要。

## 確認

実施済み:
- 変更・新規C# 7ファイルの構文チェック。
- 依存先を代替した検証で23項目を確認。実コードの店内会話・Fungusコマンドと、
  ShopUIManagerから抽出した対象メソッドを使用。
- 条件の逆順評価/AND、デフォルト、会話なし、存在しないブロック、無効コンポーネント、
  実行中ブロック、開始失敗、終了通知、連続実行防止、別店舗への切り替え、
  コマンド停止後に店を開き直さないこと、メニューの表示/非表示。
- 変更対象のgit diff --check。

未実施:
- Unity Editorでのコンパイル・Inspector表示・Scene/Prefabの設定変更・Play Mode動作。

Unityで確認すること:
1. 各フラグ条件と複数条件成立時に、想定した店内会話になる。
2. 「会話」を選ぶと終了挨拶なしでショップが閉じ、会話のTalkEnd後はフィールド操作へ戻る。
3. 会話なしの看板・店では「会話」が表示されず、購入/売却/やめるは使える。
4. 会話のある店から別の店へ移っても、前の会話設定を引き継がない。
5. 会話スキップ、ブロック停止、Scene遷移で重複起動や待機の取り残しがない。
6. 既存のTalkStart/TalkEnd、Flag変更、Call、Timelineがある会話の接続と実行順を確認する。
