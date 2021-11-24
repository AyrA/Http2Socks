using System;

namespace H2S
{
    public interface IValidateable
    {
        void Validate();
    }

    public class ValidationException : Exception
    {
        public ValidationException() : base()
        {

        }
        public ValidationException(string message) : base(message)
        {

        }
    }
}
