using System.Collections.ObjectModel;

namespace Catan3.Shared.Extensions
{
    public static class ObservableCollectionExtensions
    {
        /// <summary>
        /// Adds a range of items to an ObservableCollection
        /// </summary>
        /// <typeparam name="T">The type of items in the collection</typeparam>
        /// <param name="collection">The ObservableCollection to add items to</param>
        /// <param name="items">The items to add</param>
        public static void AddRange<T>(this ObservableCollection<T> collection, IEnumerable<T> items)
        {
            foreach (var item in items)
            {
                collection.Add(item);
            }
        }
    }
}
