using System;

namespace AttemptController.DataStructures
{

    public static class EditDistance
    {
        public class Costs
        {
            public float CaseChange;
            public float Add;
            public float Delete;
            public float Transpose;
            public float Substitute;

            public Costs(float caseChange = 0.5f,
                         float add = 1,
                         float delete = 1,
                         float transpose = 1,
                         float substitute = 1)
            {
                CaseChange = caseChange;
                Transpose = transpose;
                Add = add;
                Delete = delete;
                Substitute = substitute;
            }
        }

        public static float Calculate(string sourceString, string destinationString, Costs costs = null)
        {
            return Calculate(sourceString.ToCharArray(), destinationString.ToCharArray(), costs);
        }

        public static float Calculate(char[] xstr,
            char[] ystr,
            Costs costs = null)
        {
            if (costs == null)
                costs = new Costs();

            int xSize = xstr.Length;
            int ySize = ystr.Length;

            float[,] costMatrix = new float[xSize + 1, ySize + 1];

            costMatrix[0, 0] = 0;
            for (int x = 1; x <= xSize; x++) {
                costMatrix[x, 0] = costs.Delete * x;
            }
            for (int y = 1; y <= ySize; y++)
            {
                costMatrix[0, y] = costs.Add * y;
            }


            for (int x = 1; x <= xSize; x++)
            {
                for (int y = 1; y <= ySize; y++)
                {
                    char xChar = xstr[x - 1];
                    char yChar = ystr[y - 1];

                    float cost = costMatrix[x - 1, y - 1] + costs.Substitute;

                    if (xChar == yChar)
                    {
                        cost = Math.Min(cost, costMatrix[x - 1, y - 1]);
                    } else if (char.ToLower(xChar) == char.ToLower(yChar))
                    {
                        cost = Math.Min(cost, costMatrix[x - 1, y - 1] + costs.CaseChange);
                    }

                    cost = Math.Min(cost, costMatrix[x - 1, y] + costs.Delete);
                    
                    cost = Math.Min(cost, costMatrix[x, y - 1] + costs.Add);

                    if (x >= 2 && y >= 2 && xstr[x - 2] == yChar && ystr[y - 2] == xChar)
                    {
                        cost = Math.Min(cost, costMatrix[x - 2, y - 2] + costs.Transpose);
                    }

                    costMatrix[x,y] = cost;
                }
            }

            return costMatrix[xstr.Length, ystr.Length];
        }


    }
}
