using System.Collections.Generic;
using UnityEngine;

public class GridTouchManager : MonoBehaviour
{
    public LineRenderer lineRenderer; // LineRenderer для отрисовки линии
    public LayerMask cellLayerMask;   // Слой, на котором находятся ячейки

    public WorldGridLayout gridLayout;

    public GameScript gameScript;

    // Список выбранных ячеек
    private List<Cell> selectedCells = new List<Cell>();
    private bool isDragging = false;

    void Update()
    {
        // Обработка начала нажатия
        if (Input.GetMouseButtonDown(0))
        {
            Vector2 worldPoint = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            RaycastHit2D hit = Physics2D.Raycast(worldPoint, Vector2.zero, 0f, cellLayerMask);
            if (hit.collider != null)
            {
                Cell cell = hit.collider.GetComponent<Cell>();
                if (cell != null)
                {
                    selectedCells.Clear();
                    selectedCells.Add(cell);
                    isDragging = true;
                    UpdateLineRenderer(worldPoint);
                }
            }
        }
        // Обработка перетаскивания
        else if (Input.GetMouseButton(0) && isDragging)
{
    Vector2 worldPoint = Camera.main.ScreenToWorldPoint(Input.mousePosition);
    RaycastHit2D hit = Physics2D.Raycast(worldPoint, Vector2.zero, 0f, cellLayerMask);
    if (hit.collider != null)
    {
        Cell cell = hit.collider.GetComponent<Cell>();
        if (cell != null)
        {
            // Если ячейка уже выбрана
            if (selectedCells.Contains(cell))
            {
                // Если пользователь возвращается к предыдущей ячейке (то есть ячейка равна предпоследней в списке)
                if (selectedCells.Count > 1 && cell == selectedCells[selectedCells.Count - 2])
                {
                    // Удаляем последний элемент из выделения
                    selectedCells.RemoveAt(selectedCells.Count - 1);
                }
            }
            else
            {
                // Добавляем ячейку, если она соседняя с последней и имеет тот же тип, что и первая
                if (IsAdjacent(cell, selectedCells[selectedCells.Count - 1]) && cell.cellType == selectedCells[0].cellType)
                {
                    selectedCells.Add(cell);
                }
            }
        }
    }
    // Обновляем линию с текущей позицией пальца
    UpdateLineRenderer(worldPoint);
}
        // Обработка окончания нажатия
        else if (Input.GetMouseButtonUp(0) && isDragging)
        {
            isDragging = false;
            ClearLineRenderer();

            

            if (selectedCells.Count > 1){
                StartCoroutine(gridLayout.ProcessSelection(selectedCells));
                if(selectedCells[0] != null){
                    gameScript.chekWin(selectedCells, selectedCells[0].cellType, selectedCells.Count);
                }
            }

            // Здесь можно добавить логику для обработки выбранных ячеек (например, удаление)
            selectedCells.Clear();
        }
    }

    // Проверка, что две ячейки соседние по горизонтали или вертикали
    bool IsAdjacent(Cell cellA, Cell cellB)
    {
        int dx = Mathf.Abs(cellA.gridX - cellB.gridX);
        int dy = Mathf.Abs(cellA.gridY - cellB.gridY);
        return (dx == 1 && dy == 0) || (dx == 0 && dy == 1);
    }

    // Обновление LineRenderer'а: соединяет позиции выбранных ячеек и текущую позицию пальца
    void UpdateLineRenderer(Vector2 currentFingerPos)
    {
        int count = selectedCells.Count;
        lineRenderer.positionCount = count + 1;
        for (int i = 0; i < count; i++)
        {
            lineRenderer.SetPosition(i, selectedCells[i].transform.position);
        }
        lineRenderer.SetPosition(count, currentFingerPos);
    }

    void ClearLineRenderer()
    {
        lineRenderer.positionCount = 0;
    }
}