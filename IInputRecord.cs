using System.Collections.Generic;

namespace Octagon.Formatik
{
    public interface IInputRecord
    {
        int Index { get; }
        
        string GetToken(string tokenSelector);
        
        /// <summary>
        /// Returns a list of unique fieldSelectors which can be used to retreave the supplied token.
        /// The Implemented method should search the record's full space and throw exception if it finds multiple selectors which can return the token  
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        IEnumerable<string> GetFieldSelectors(string token);
        IEnumerable<Token> GetTokens();
    }
}