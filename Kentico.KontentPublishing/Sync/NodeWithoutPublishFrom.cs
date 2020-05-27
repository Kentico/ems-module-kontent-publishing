using System.Collections.Generic;

using CMS.DocumentEngine;
using CMS.Base;

namespace Kentico.EMS.Kontent.Publishing
{
    internal class NodeWithoutPublishFrom : IDataContainer
    {
        private readonly TreeNode _node;
        
        public NodeWithoutPublishFrom(TreeNode node)
        {
            _node = node;
        }

        public object this[string columnName]
        {
            get => _node[columnName];
            set => _node[columnName] = value;
        }

        public List<string> ColumnNames => _node.ColumnNames;

        public bool ContainsColumn(string columnName)
        {
            return _node.ContainsColumn(columnName);
        }

        public object GetValue(string columnName)
        {
            TryGetValue(columnName, out object value);

            return value;
        }

        public bool SetValue(string columnName, object value)
        {
            return _node.SetValue(columnName, value);
        }

        public bool TryGetValue(string columnName, out object value)
        {
            bool result = _node.TryGetValue(columnName, out value);

            // We need to ignore publish from in order to publish also scheduled pages
            if (columnName.EqualsCSafe("DocumentPublishFrom", true))
            {
                value = null;
            }

            return result;
        }
    }
}
