open System.IO
open System
open System.Reflection
open System.Net

open Shared

open Suave
open Suave.Files
open Suave.Successful
open Suave.Filters
open Suave.Operators

open Fable.Remoting.Server
open Fable.Remoting.Suave

open System.Data
open System.Data.SqlClient
open MySql.Data.MySqlClient

module ServerPath =
    let workingDirectory =
        let currentAsm = Assembly.GetExecutingAssembly()
        let codeBaseLoc = currentAsm.CodeBase
        let localPath = Uri(codeBaseLoc).LocalPath
        Directory.GetParent(localPath).FullName

    let resolve segments =
        let paths = Array.concat [| [| workingDirectory |]; Array.ofList segments |]
        Path.GetFullPath(Path.Combine(paths))

let tryGetEnv = System.Environment.GetEnvironmentVariable >> function null | "" -> None | x -> Some x
let publicPath = ServerPath.resolve [".."; "Client"; "public"]
let port = tryGetEnv "HTTP_PLATFORM_PORT" |> Option.map System.UInt16.Parse |> Option.defaultValue 8085us

let establishConnection () = 
    let connectionString =
        let cStringPath =
            ServerPath.resolve ["."; "connectionstring.txt"]

        seq {
            use sr = new System.IO.StreamReader (cStringPath)
            while not sr.EndOfStream do
                yield sr.ReadLine ()
        }
        |> String.concat ""
    //printfn "establishing connection?"
    new MySqlConnection(connectionString)

        
let checkPin (pin: string) =
    use connection = establishConnection()
    connection.Open()
    let command = connection.CreateCommand()
    command.CommandText <- "SELECT * FROM Person WHERE Pin = @pin"
    let p = command.Parameters.Add ("@pin", MySqlDbType.Int32) 
    p.Value <- int pin
    let reader = command.ExecuteReader()
    let person = 
        match (reader.Read()) with
        |true ->    

            let pin' = reader.GetInt32(1)
            let name = reader.GetString(2)
            let email = reader.GetString(3)
            let status = reader.GetByte(4)

            {
                Pin=string pin
                Name=name
                Email=email
                Status=string status
            }
        |false -> failwith "No matching pin in database"
    person

let getBalance (userName:string) =
    use connection = establishConnection()
    connection.Open()
    //get person Id for foreign key
    let getUserCmd = connection.CreateCommand()
    getUserCmd.CommandText <- "SELECT Id FROM Person WHERE Name = @userName"
    let n = getUserCmd.Parameters.Add ("@userName", MySqlDbType.VarChar) 
    n.Value <- userName
    let reader = getUserCmd.ExecuteReader()
    let personId = 
        match (reader.Read()) with
        |true ->    
            let pid = Some (reader.GetInt64(0))
            printfn "%A" pid
            pid
        |false 
            ->  printfn "no PersonId"
                None
    reader.Close()

    //Get balance of found user id
    match personId with
    |Some id ->
        let getBalanceCmd = connection.CreateCommand()
        getBalanceCmd.CommandText <- "SELECT Balance from CurrentBalance WHERE FK_Person = @pid"
        let pid = getBalanceCmd.Parameters.Add ("@pid", MySqlDbType.Int32) 
        pid.Value <- id
        let reader = getBalanceCmd.ExecuteReader()
        match (reader.Read()) with
        |true ->    
            let bal = (reader.GetDouble(0))
            printfn "Balance : %f" bal
            reader.Close()
            Some bal
        
        |false 
            ->  printfn "no Balance"
                reader.Close()
                None
    |_ -> None

let tick (userName:string) (tradeName:string) (amount: int) (extId:string) =

    use connection = establishConnection()
    connection.Open()

    //Get trade Name from extended id
    printfn "searching for trade name with extended id %s" extId
    let getTradeNameCommand = connection.CreateCommand()
    getTradeNameCommand.CommandText <- "SELECT Name FROM Trade WHERE ExtId = @extId"
    let n = getTradeNameCommand.Parameters.Add ("@extId", MySqlDbType.VarChar) 
    n.Value <- extId
    let reader = getTradeNameCommand.ExecuteReader()
    let tradeName' = 
        match (reader.Read()) with
        |true ->    
            let pid = Some (reader.GetString(0))
            printfn "found tradename %A" pid
            pid
        |false 
            ->  printfn "no TradeNameFound"
                None
    reader.Close()    

    //get person Id for foreign key
    let getUserCmd = connection.CreateCommand()
    getUserCmd.CommandText <- "SELECT Id FROM Person WHERE Name = @userName"
    let n = getUserCmd.Parameters.Add ("@userName", MySqlDbType.VarChar) 
    n.Value <- userName
    let reader = getUserCmd.ExecuteReader()
    let personId = 
        match (reader.Read()) with
        |true ->    
            let pid = Some (reader.GetInt64(0))
            printfn "%A" pid
            pid
        |false 
            ->  printfn "no PersonId"
                None
    reader.Close()    

    //get trade id for foreign key
    let getTradeCmd = connection.CreateCommand()        
    getTradeCmd.CommandText <- "SELECT Id FROM Trade WHERE ExtId = @extId"
    let tn = getTradeCmd.Parameters.Add ("@extId", MySqlDbType.VarChar) 
    tn.Value <- extId
    let reader = getTradeCmd.ExecuteReader()
    let tradeId = 
        match (reader.Read()) with
        |true ->    
            let tid = Some (reader.GetInt64(0))
            printfn "%A"tid
            tid
        |false 
            ->  printfn "no TradeId"
                None
    reader.Close()        

    //do insert if both ids exist
    match (personId,tradeId) with
    |(Some pId, Some tId) ->        
        let insertTransactionCmd = connection.CreateCommand()
        insertTransactionCmd.CommandText <-"INSERT into Orders (FK_Person,FK_Trade,Amount,Time)
                                            VALUES (@pId,@tId,@amt,@time)"
        let p = insertTransactionCmd.Parameters.Add("@pId",MySqlDbType.Int32)
        let t = insertTransactionCmd.Parameters.Add("@tId",MySqlDbType.Int64)
        let amt = insertTransactionCmd.Parameters.Add("@amt",MySqlDbType.Int32)
        let time = insertTransactionCmd.Parameters.Add("@time",MySqlDbType.DateTime)
        p.Value <- pId
        t.Value <- tId
        amt.Value <- amount
        time.Value <- System.DateTime.Now
        match insertTransactionCmd.ExecuteNonQuery() with
        | -1 -> failwith "soos"
        | _ -> userName, amount, tradeName'.Value
    |_ -> failwith "saas"

let config =
    { defaultConfig with
        homeFolder = Some publicPath
        bindings = [ HttpBinding.create HTTP (System.Net.IPAddress.Parse "0.0.0.0") port ] }

let csbarApi : ICSBarApi = {
    ConfirmPin =
        fun pin ->
            async {
                return (checkPin pin)
            }
    GetBalance =
        fun usrName ->
            async {
                return (getBalance usrName) |> Option.get
        }
    Tick = 
        fun userName tradeName amount extid ->
            async {
                 return tick userName tradeName amount extid
        }

}

// Custom error will be propagated back to client
type CustomError = { errorMsg: string }

let errorHandler (ex: Exception) (routeInfo: RouteInfo<HttpContext>) = 
    // do some logging
    printfn "Error at %s on method %s" routeInfo.path routeInfo.methodName
    // decide whether or not you want to propagate the error to the client
    Propagate ex.Message


let webApi =
    Remoting.createApi()
    |> Remoting.withRouteBuilder Route.builder
    |> Remoting.withDiagnosticsLogger (printfn "%s")
    |> Remoting.fromValue csbarApi
    |> Remoting.withErrorHandler errorHandler
    |> Remoting.buildWebPart

let webApp =
    choose [
        webApi
        path "/" >=> browseFileHome "index.html"
        browseHome
        RequestErrors.NOT_FOUND "Not found!"
    ]

startWebServer config webApp