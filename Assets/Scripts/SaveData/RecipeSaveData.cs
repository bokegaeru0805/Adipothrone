using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 1つのレシピの保存データ
/// </summary>
[Serializable]
public class RecipeEntry
{
    public int recipeID; // レシピのID
    public int craftCount; // これまでに合成を実行した回数
    public bool isUnlocked; // 解放済みかどうか
    public bool isNew; // UIで「NEW!」などを表示するための確認フラグ

    public RecipeEntry(int id, bool unlocked = true)
    {
        recipeID = id;
        craftCount = 0;
        isUnlocked = unlocked;
        isNew = unlocked; // 解放された瞬間はNEW扱いにする
    }
}

/// <summary>
/// レシピに関するセーブデータをまとめたクラス
/// </summary>
[Serializable]
public class RecipeSaveData
{
    // 認識している全レシピのリスト（未解放のものも含むようになる）
    public List<RecipeEntry> knownRecipes = new List<RecipeEntry>();

    /// <summary>
    /// レシピを解放します
    /// </summary>
    /// <param name="recipeID">解放するレシピのID（Enum）</param>
    public void UnlockRecipe(Enum recipeID)
    {
        int recipeIDNumber = EnumIDUtility.ToID(recipeID);
        var recipe = knownRecipes.Find(r => r.recipeID == recipeIDNumber);
        if (recipe != null)
        {
            // 既にリストに存在する場合（「???」表示などで登録済みの場合）
            if (!recipe.isUnlocked)
            {
                recipe.isUnlocked = true;
                recipe.isNew = true; // 新しく解放されたのでフラグを立てる
            }
        }
        else
        {
            // リストにない場合は新規追加して解放
            knownRecipes.Add(new RecipeEntry(recipeIDNumber, true));
        }
    }

    /// <summary>
    /// 未解放状態（ヒントや???表示用）としてレシピをリストに登録します
    /// </summary>
    /// <param name="recipeID">登録するレシピのID（Enum）</param>
    public void DiscoverRecipe(Enum recipeID)
    {
        int recipeIDNumber = EnumIDUtility.ToID(recipeID);
        if (!knownRecipes.Exists(r => r.recipeID == recipeIDNumber))
        {
            knownRecipes.Add(new RecipeEntry(recipeIDNumber, false));
        }
    }

    /// <summary>
    /// 合成実行回数を増やします
    /// </summary>
    /// <param name="recipeID">合成したレシピのID（Enum）</param>
    /// <param name="addAmount">追加する回数</param>
    public void AddCraftCount(Enum recipeID, int addAmount = 1)
    {
        int recipeIDNumber = EnumIDUtility.ToID(recipeID);
        var recipe = knownRecipes.Find(r => r.recipeID == recipeIDNumber);
        if (recipe != null)
        {
            recipe.craftCount += addAmount;
        }
    }

    /// <summary>
    /// これまでに合成した回数を取得します
    /// </summary>
    /// <param name="recipeID">取得するレシピのID（Enum）</param>
    /// <returns>合成した回数</returns>
    public int GetCraftCount(Enum recipeID)
    {
        int recipeIDNumber = EnumIDUtility.ToID(recipeID);
        var recipe = knownRecipes.Find(r => r.recipeID == recipeIDNumber);
        return recipe != null ? recipe.craftCount : 0;
    }

    /// <summary>
    /// レシピが既に解放されているか判定します
    /// </summary>
    /// <param name="recipeID">判定するレシピのID（Enum）</param>
    /// <returns>解放済みであればtrue</returns>
    public bool IsRecipeUnlocked(Enum recipeID)
    {
        int recipeIDNumber = EnumIDUtility.ToID(recipeID);
        var recipe = knownRecipes.Find(r => r.recipeID == recipeIDNumber);
        return recipe != null && recipe.isUnlocked;
    }

    /// <summary>
    /// NEWフラグをオフにします（UI画面でレシピを確認した時などに呼ぶ）
    /// </summary>
    /// <param name="recipeID">確認したレシピのID（Enum）</param>
    public void MarkAsSeen(Enum recipeID)
    {
        int recipeIDNumber = EnumIDUtility.ToID(recipeID);
        var recipe = knownRecipes.Find(r => r.recipeID == recipeIDNumber);
        if (recipe != null)
        {
            recipe.isNew = false;
        }
    }

    ///<summary>
    /// レシピの個数を取得します
    ///</summary>
    /// <param name="recipeID">取得するレシピのID（Enum）</param>
    /// <returns>レシピの個数</returns>
    public int GetRecipeAmount(Enum recipeID)
    {
        int recipeIDNumber = EnumIDUtility.ToID(recipeID);
        var recipe = knownRecipes.Find(r => r.recipeID == recipeIDNumber);
        return recipe != null && recipe.isUnlocked ? 1 : 0;
    }
}
