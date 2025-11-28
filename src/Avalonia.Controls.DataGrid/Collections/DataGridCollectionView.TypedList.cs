// (c) Copyright Microsoft Corporation.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

#nullable disable

using System.ComponentModel;

namespace Avalonia.Collections
{
    sealed partial class DataGridCollectionView
    {
        PropertyDescriptorCollection ITypedList.GetItemProperties(PropertyDescriptor[] listAccessors)
        {
            // Prefer the binding list if it exposes ITypedList (e.g. DataView for DataTable).
            if (_bindingList is ITypedList bindingTypedList)
            {
                return bindingTypedList.GetItemProperties(listAccessors);
            }

            if (SourceCollection is ITypedList sourceTypedList)
            {
                return sourceTypedList.GetItemProperties(listAccessors);
            }

            // Fall back to descriptors from a representative item or the item type.
            var representative = GetRepresentativeItem();
            if (representative != null)
            {
                return TypeDescriptor.GetProperties(representative);
            }

            var itemType = ItemType;
            if (itemType != null)
            {
                return TypeDescriptor.GetProperties(itemType);
            }

            return new PropertyDescriptorCollection(null);
        }

        string ITypedList.GetListName(PropertyDescriptor[] listAccessors)
        {
            if (_bindingList is ITypedList bindingTypedList)
            {
                return bindingTypedList.GetListName(listAccessors);
            }

            if (SourceCollection is ITypedList sourceTypedList)
            {
                return sourceTypedList.GetListName(listAccessors);
            }

            return string.Empty;
        }
    }
}
