using UnityEngine;
using TMPro;

public class UpdateEvaluationUI : MonoBehaviour
{
    [SerializeField] private AI ai;
    [SerializeField] private TMP_Text MaterialAdvantage;
    [SerializeField] private TMP_Text MyEngineEvaluation;
    [SerializeField] private TMP_Text StockfishEvaluation;

    private void Update()
    {
        MaterialAdvantage.text = "Material Advantage" + ai.MaterialValueEvaluation().ToString();
        MyEngineEvaluation.text = "MyEngine's Evaluation: " + ai.EvaluateBoard().ToString();
        
        if(StockfishTester.Instance != null)
        {
            StockfishEvaluation.text = "StockfishEvaluation: " + StockfishTester.Instance.stockfishEvalString.ToString();
            
        }
        else
        {
            StockfishEvaluation.gameObject.SetActive(false);
        }
    }
}
