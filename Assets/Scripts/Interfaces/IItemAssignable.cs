using System;

public interface IItemAssignable
{
    public Enum AssignedItemID { get; }
    public void AssignItem(Enum itemID);
}