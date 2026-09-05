using System;

/// <summary>
/// ver 1.3.0相当のデータ更新を行います。
/// </summary>
public sealed class SaveDataMigrationV3 : ISaveDataMigration
{
    private const int IncorrectNormalV2RecipeID = 10001;
    private const int NormalV2RecipeID = (int)RecipeItemName.Normal_v2;

    public int TargetVersion => 3;

    public void Migrate(SaveData saveData)
    {
        if (saveData == null)
            throw new ArgumentNullException(nameof(saveData));

        // ver 1.3.0向けの更新処理は、追加順が分かるようにここから個別メソッドを呼ぶ。
        CorrectNormalV2RecipeID(saveData.RecipeData);
    }

    /// <summary>
    /// 誤って10001として保存されたNormal_v2のレシピIDを11001へ修正します。
    /// </summary>
    private static void CorrectNormalV2RecipeID(RecipeSaveData recipeData)
    {
        if (recipeData?.knownRecipes == null)
            return;

        RecipeEntry correctedEntry = null;

        for (int i = recipeData.knownRecipes.Count - 1; i >= 0; i--)
        {
            RecipeEntry entry = recipeData.knownRecipes[i];
            if (
                entry == null
                || (
                    entry.recipeID != IncorrectNormalV2RecipeID
                    && entry.recipeID != NormalV2RecipeID
                )
            )
            {
                continue;
            }

            if (correctedEntry == null)
            {
                entry.recipeID = NormalV2RecipeID;
                correctedEntry = entry;
                continue;
            }

            MergeRecipeEntry(correctedEntry, entry);
            recipeData.knownRecipes.RemoveAt(i);
        }
    }

    private static void MergeRecipeEntry(RecipeEntry destination, RecipeEntry source)
    {
        destination.craftCount = Math.Max(destination.craftCount, source.craftCount);
        destination.isUnlocked |= source.isUnlocked;
        destination.isNew |= source.isNew;
    }
}
