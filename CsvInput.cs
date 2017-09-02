namespace Octagon.Formatik
{
    public class CsvInput: DelimitedInput
    {
        private static Input instance;

        public static Input Factory() {
            if (instance == null)
                instance = new CsvInput();

            return instance;
        }
        
        protected override string GetDelimiter()
        {
            return ",";
        }
    }
}