#nullable enable
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DeskNest.Views;

/// <summary>
/// Mouse behavior shared by every icon grid: open on double-click, drag selected items out as files,
/// and keep a multi-selection intact when a drag starts on one of the selected items.
/// </summary>
public sealed class TileList
{
    /// <summary>Data format marking a drag that started inside DeskNest; its value is the source list's owner id.</summary>
    public const string InternalFormat = "DeskNest.Internal";

    private readonly ListBox _list;
    private readonly string _ownerId;
    private Point _start;
    private ListBoxItem? _pressed;
    private bool _deferSelect;

    public TileList(ListBox list, string ownerId)
    {
        _list = list;
        _ownerId = ownerId;
        list.PreviewMouseLeftButtonDown += OnDown;
        list.PreviewMouseMove += OnMove;
        list.PreviewMouseLeftButtonUp += OnUp;
        // Right-clicking empty space should offer the frame menu, not act on a stale selection.
        list.PreviewMouseRightButtonDown += (_, e) =>
        {
            if (ItemsControl.ContainerFromElement(_list, e.OriginalSource as DependencyObject) is null)
                _list.UnselectAll();
        };
        list.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && list.SelectedItem is ItemView item)
            {
                Activated?.Invoke(item);
                e.Handled = true;
            }
        };
    }

    public event Action<ItemView>? Activated;

    public IReadOnlyList<ItemView> Selected => _list.SelectedItems.Cast<ItemView>().ToList();

    public ItemView? ItemAt(object? originalSource) =>
        (ItemsControl.ContainerFromElement(_list, originalSource as DependencyObject) as ListBoxItem)?.Content as ItemView;

    /// <summary>Index of the tile under <paramref name="e"/>'s position, or the end of the list.</summary>
    public int DropIndex(DragEventArgs e)
    {
        var container = ItemsControl.ContainerFromElement(_list, e.OriginalSource as DependencyObject) as ListBoxItem;
        return container is null ? _list.Items.Count : _list.ItemContainerGenerator.IndexFromContainer(container);
    }

    public static bool IsInternal(IDataObject data, string ownerId) =>
        data.GetDataPresent(InternalFormat) && data.GetData(InternalFormat) as string == ownerId;

    private void OnDown(object sender, MouseButtonEventArgs e)
    {
        _pressed = ItemsControl.ContainerFromElement(_list, e.OriginalSource as DependencyObject) as ListBoxItem;
        _start = e.GetPosition(_list);
        _deferSelect = false;
        if (_pressed is null)
        {
            _list.UnselectAll();
            return;
        }

        if (e.ClickCount == 2 && _pressed.Content is ItemView item)
        {
            Activated?.Invoke(item);
            e.Handled = true;
            return;
        }

        // Clicking an already-selected tile without modifiers: keep the selection so a multi-item drag works,
        // and collapse it to this tile on mouse-up if no drag happened.
        if (_pressed.IsSelected && Keyboard.Modifiers == ModifierKeys.None && _list.SelectedItems.Count > 1)
        {
            _deferSelect = true;
            e.Handled = true;
        }
    }

    private void OnUp(object sender, MouseButtonEventArgs e)
    {
        if (_deferSelect && _pressed is not null)
        {
            _list.SelectedItems.Clear();
            _pressed.IsSelected = true;
        }
        _deferSelect = false;
        _pressed = null;
    }

    private void OnMove(object sender, MouseEventArgs e)
    {
        if (_pressed is null || e.LeftButton != MouseButtonState.Pressed)
            return;
        var delta = e.GetPosition(_list) - _start;
        if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        if (!_pressed.IsSelected)
        {
            _list.SelectedItems.Clear();
            _pressed.IsSelected = true;
        }
        _pressed = null;
        _deferSelect = false;

        var paths = Selected.Where(i => i.Exists).Select(i => i.Path).ToArray();
        if (paths.Length == 0)
            return;
        var data = new DataObject(DataFormats.FileDrop, paths);
        data.SetData(InternalFormat, _ownerId);
        DragDrop.DoDragDrop(_list, data, DragDropEffects.Copy | DragDropEffects.Move | DragDropEffects.Link);
    }
}
