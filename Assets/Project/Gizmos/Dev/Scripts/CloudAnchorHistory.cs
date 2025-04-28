namespace Gizmos
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    [Serializable]
    public struct CloudAnchorHistory
    {
        public string Name;

        public string Id;

        public string SerializedTime;

        public CloudAnchorHistory(string name, string id, DateTime time)
        {
            Name = name;
            Id = id;
            SerializedTime = time.ToString();
        }

        public CloudAnchorHistory(string name, string id) : this(name, id, DateTime.Now)
        {
        }

        public DateTime CreatedTime
        {
            get
            {
                return Convert.ToDateTime(SerializedTime);
            }
        }

        public override string ToString()
        {
            return JsonUtility.ToJson(this);
        }
    }

    [Serializable]
    public class CloudAnchorHistoryCollection
    {
        public List<CloudAnchorHistory> Collection = new List<CloudAnchorHistory>();
    }
}
