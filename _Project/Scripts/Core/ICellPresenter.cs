namespace GridEmpire.Core
{
    public interface ICellPresenter
    {
        void Initialize(CellData data);
        void UpdateVisual();
        void SetSelected(bool isSelected);
    }
}