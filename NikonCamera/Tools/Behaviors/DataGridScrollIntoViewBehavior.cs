using Microsoft.Xaml.Behaviors;
using System.Windows.Controls;

namespace NikonCamera.Tools.Behaviors;

public class DataGridScrollIntoViewBehavior : Behavior<DataGrid>
{
    protected override void OnAttached()
    {
        base.OnAttached();

        AssociatedObject.SelectionChanged += AssociatedObject_SelectionChanged;
    }

    private void AssociatedObject_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var dataGrid = sender as DataGrid;

        if (dataGrid?.SelectedItem != null)
        {
            dataGrid.Dispatcher.Invoke(() =>
            {
                dataGrid.UpdateLayout();
                dataGrid.ScrollIntoView(dataGrid.SelectedItem, null);
            });
        }
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();

        AssociatedObject.SelectionChanged -= AssociatedObject_SelectionChanged;
    }
}