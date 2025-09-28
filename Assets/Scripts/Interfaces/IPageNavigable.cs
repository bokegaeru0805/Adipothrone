using System.Collections.Generic;
using UnityEngine.UI;

public interface IPageNavigable
{
    List<Button> LeftSideButtons { get; }
    List<Button> RightSideButtons { get; }
    int Page { get; set; }

    bool TryAssignItemsToPage(int pageNumber, int previousRow, bool moveRight);
}
