using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreBankingTest.APP.Common.Models
{
    public record Result
    {
        public bool IsSuccess {  get; set; }
        public string[] Errors { get; set; } = Array.Empty<string>();

        public static Result Success() => new() { IsSuccess = true };
        public static Result Failure(params string[] errors) => new() { IsSuccess = false ,Errors = errors };


    }

    public record Result<T> : Result { 
     public T? Data { get; init; }
        public static Result<T> Success (T data ) => new() { IsSuccess = true,Data = data };
        public static new Result<T> Failure (params string[] errors) => new() {IsSuccess = false ,Errors = errors };
    
    }

}
