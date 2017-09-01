namespace Octagon.Formatik
{
    public class TsvInput : DelimitedInput
    {
        private static Input instance;

        public static Input Factory()
        {
            if (instance == null)
                instance = new TsvInput();

            return instance;
        }

        protected override string GetDelimiter()
        {
            return "\t";
        }
    }
}