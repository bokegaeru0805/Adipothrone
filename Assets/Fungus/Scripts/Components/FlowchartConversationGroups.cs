using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fungus
{
    /// <summary>
    /// Flowchartウィンドウ上でBlockを会話単位に整理するための編集用データです。
    /// 会話の実行順や接続には影響しません。
    /// </summary>
    [AddComponentMenu("")]
    public class FlowchartConversationGroups : MonoBehaviour
    {
        [Serializable]
        public class Member
        {
            public Block block;
            public int column;
            public int order;
        }

        [Serializable]
        public class Group
        {
            public string title = "Character";
            public Color color = new Color(0.2f, 0.55f, 0.85f, 0.18f);
            public Vector2 contentPosition;
            public List<Member> members = new List<Member>();
        }

        [SerializeField]
        private List<Group> groups = new List<Group>();

        public List<Group> Groups => groups;
    }
}
