using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;

public class WorldGridLayout : MonoBehaviour
{
    [Header("Параметры сетки")]
    public int columns = 5;
    public int rows = 5;
    public float horizontalMargin = 1.0f; // Отступы слева и справа (в мировых единицах)
    public float spacing = 0.2f;          // Промежуток между ячейками
    public GameObject[] cellPrefabs;      // Массив префабов ячеек

    [Header("Параметры анимации")]
    public float animationDuration = 0.3f; // Длительность анимаций

    private float cellSize;
    private float leftEdge;
    private float bottomEdge;

    // Матрица для хранения ячеек
    private Cell[,] grid;

    void Start()
    {
        
    }

    public void NewGame(){
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Destroy(transform.GetChild(i).gameObject);
        }
        InitializeGrid();
    }

    // Создаем матрицу и размещаем ячейки
    void InitializeGrid()
    {
        float screenWidthWorld = 2 * Camera.main.orthographicSize * Camera.main.aspect;
        float totalSpacing = spacing * (columns - 1);
        float availableWidth = screenWidthWorld - (2 * horizontalMargin) - totalSpacing;
        cellSize = availableWidth / columns;

        leftEdge = Camera.main.transform.position.x - screenWidthWorld / 2 + horizontalMargin + cellSize / 2;
        float totalGridHeight = rows * cellSize + (rows - 1) * spacing;
        bottomEdge = Camera.main.transform.position.y - totalGridHeight / 2 + cellSize / 2;

        grid = new Cell[columns, rows];

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                Vector3 pos = ComputeCellPosition(col, row);
                int randomIndex = Random.Range(0, cellPrefabs.Length);
                GameObject newCell = Instantiate(cellPrefabs[randomIndex], pos, Quaternion.identity, transform);
                newCell.transform.localScale = new Vector3(cellSize, cellSize, 1);

                Cell cellComponent = newCell.GetComponent<Cell>();
                if (cellComponent != null)
                {
                    cellComponent.gridX = col;
                    cellComponent.gridY = row;
                    cellComponent.cellType = randomIndex;
                    grid[col, row] = cellComponent;
                }
            }
        }
    }

    // Вычисляем позицию ячейки по координатам сетки
    public Vector3 ComputeCellPosition(int col, int row)
    {
        float posX = leftEdge + col * (cellSize + spacing);
        float posY = bottomEdge + row * (cellSize + spacing);
        return new Vector3(posX, posY, 0);
    }

    // Метод, вызываемый при отпускании пальца, обрабатывает удаление выбранных ячеек,
    // последующий обвал и заполнение пустых мест
    public IEnumerator ProcessSelection(List<Cell> selectedCells)
    {

        Cell[] cells = new Cell[selectedCells.Count];
        for(int i = 0; i < selectedCells.Count; i++){
            cells[i] = selectedCells[i];
        }

        if (selectedCells == null || selectedCells.Count == 0)
            yield break;

        // 1. Анимированное удаление выбранных ячеек
        foreach (Cell cell in selectedCells)
        {
            cell.transform.DOScale(0, animationDuration);
            Debug.Log("скеилю");
        }
        yield return new WaitForSeconds(animationDuration);
        Debug.Log("Удаляю");

        foreach (Cell cell in cells)
        {
            if(cell != null){
                grid[cell.gridX, cell.gridY] = null;
                Destroy(cell.gameObject);
            }
        }

        // 2. Обвал оставшихся ячеек
        yield return StartCoroutine(CollapseColumns());

        // 3. Заполнение пустых мест новыми ячейками
        yield return StartCoroutine(FillEmptyCells());
    }

    // Обновленный метод обвала: проверяем каждую колонку циклом while,
    // пока не перестанут находиться пустые ячейки, за которыми есть ячейки сверху
    private IEnumerator CollapseColumns()
    {
    for (int col = 0; col < columns; col++)
    {
        int gapCount = 0;
        // Проходим снизу вверх (так как row 0 – самый нижний ряд)
        for (int row = 0; row < rows; row++)
        {
            if (grid[col, row] == null)
            {
                gapCount++;
            }
            else if (gapCount > 0)
            {
                Cell fallingCell = grid[col, row];
                int targetRow = row - gapCount; // Новая позиция с учётом пустых ячеек под ней
                grid[col, targetRow] = fallingCell;
                grid[col, row] = null;
                fallingCell.gridY = targetRow;
                Vector3 targetPos = ComputeCellPosition(col, targetRow);
                fallingCell.transform.DOMove(targetPos, animationDuration);
            }
        }
    }
    yield return new WaitForSeconds(animationDuration);
    }

    // Заполнение пустых мест новыми ячейками, которые «падают» сверху
    private IEnumerator FillEmptyCells()
    {
        for (int col = 0; col < columns; col++)
        {
            for (int row = 0; row < rows; row++)
            {
                if (grid[col, row] == null)
                {
                    Vector3 startPos = ComputeCellPosition(col, rows);
                    int randomIndex = Random.Range(0, cellPrefabs.Length);
                    GameObject newCell = Instantiate(cellPrefabs[randomIndex], startPos, Quaternion.identity, transform);
                    newCell.transform.localScale = new Vector3(cellSize, cellSize, 1);
                    Cell cellComponent = newCell.GetComponent<Cell>();
                    cellComponent.gridX = col;
                    cellComponent.gridY = row;
                    cellComponent.cellType = randomIndex;
                    grid[col, row] = cellComponent;

                    Vector3 targetPos = ComputeCellPosition(col, row);
                    newCell.transform.DOMove(targetPos, animationDuration);
                }
            }
        }
        yield return new WaitForSeconds(animationDuration);
    }
}