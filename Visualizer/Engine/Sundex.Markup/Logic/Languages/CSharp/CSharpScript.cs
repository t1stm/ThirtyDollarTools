namespace Sunder.Markup.Logic.Languages.CSharp;

public class CSharpScript : SundexScript
{
    public override Action Compile(string sourceCode, object? context)
    {
        if (context is null)
            throw new ArgumentNullException(nameof(context));

        /*
         * TODO: implement C# execution logic. Should use Roslyn for the compilation and
         * a virtual environment should be made with the following properties:
         * 1. this.Context:
         *  Make sure that context is part of some interface that allows getting wrappers for the context properties.
         *  The interface should have Get(string paramName) -> Wrapper<object> and Get<T>(string paramName) -> Wrapper<T>.
         */
        throw new NotImplementedException();
    }
}