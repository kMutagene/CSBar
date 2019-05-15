namespace Shared

type Counter = int


module Route =
    /// Defines how routes are generated on server and mapped from client
    let builder typeName methodName =
        sprintf "/api/%s/%s" typeName methodName

type Person = {
    //add some hash security
    Pin     : string
    Name    : string
    Email   : string
    Status  : string
}

type ICSBarApi = {
    //add some hash security
    ConfirmPin : string -> Async<Person>
    //add some hash security
    GetBalance : string -> Async<float>
    //add some hash security
    Tick : string -> string -> int -> string -> Async<(string*int*string)>
}