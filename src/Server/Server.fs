open System.IO
open System.Net
open System.Threading

open Suave
open Suave.Files
open Suave.Successful
open Suave.Filters
open Suave.Operators

open Shared

open Fable.Remoting.Server
open Fable.Remoting.Suave

open Suave.Logging

open System.Data
open System.Data.SqlClient

let establishConnection () = 
    let connectionString =
        seq {
            use sr = new System.IO.StreamReader (__SOURCE_DIRECTORY__ + "/connectionString.txt")
            while not sr.EndOfStream do
                yield sr.ReadLine ()
        }
        |> String.concat ""
    new SqlConnection(connectionString)

let checkPin (pin: string) =
    use connection = establishConnection()
    connection.Open()
    let command = connection.CreateCommand()
    command.CommandText <- "SELECT * FROM Person WHERE Pin = @pin"
    let p = command.Parameters.Add ("@pin", SqlDbType.Int) 
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
    getUserCmd.CommandText <- "SELECT Id FROM PERSON WHERE Name = @userName"
    let n = getUserCmd.Parameters.Add ("@userName", SqlDbType.NVarChar) 
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
        let pid = getBalanceCmd.Parameters.Add ("@pid", SqlDbType.BigInt) 
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
    let n = getTradeNameCommand.Parameters.Add ("@extId", SqlDbType.NVarChar) 
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
    getUserCmd.CommandText <- "SELECT Id FROM PERSON WHERE Name = @userName"
    let n = getUserCmd.Parameters.Add ("@userName", SqlDbType.NVarChar) 
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
    let tn = getTradeCmd.Parameters.Add ("@extId", SqlDbType.NVarChar) 
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
        let p = insertTransactionCmd.Parameters.Add("@pId",SqlDbType.BigInt)
        let t = insertTransactionCmd.Parameters.Add("@tId",SqlDbType.BigInt)
        let amt = insertTransactionCmd.Parameters.Add("@amt",SqlDbType.Int)
        let time = insertTransactionCmd.Parameters.Add("@time",SqlDbType.DateTime2)
        p.Value <- pId
        t.Value <- tId
        amt.Value <- amount
        time.Value <- System.DateTime.Now
        match insertTransactionCmd.ExecuteNonQuery() with
        | -1 -> failwith "soos"
        | _ -> userName, amount, tradeName'.Value
    |_ -> failwith "saas"



let publicPath = Path.GetFullPath "../Client/public"
let port = 8085us

let config =
    { defaultConfig with
        homeFolder = Some publicPath
        bindings = [ HttpBinding.create HTTP (System.Net.IPAddress.Parse "0.0.0.0") port ] }

let getInitCounter() : Async<Counter> = async { return 42 }

    
let testUpperCaseJson = """{
  "Pin": "1337",
  "Name": "lol",
  "Email": "ol",
  "Status": "o"
}"""

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


let webApi =
    Remoting.createApi()
    |> Remoting.withRouteBuilder Route.builder
    |> Remoting.withDiagnosticsLogger (printfn "%s")
    |> Remoting.fromValue csbarApi
    |> Remoting.buildWebPart

let webApp =
    choose [
        webApi
        path "/" >=> browseFileHome "index.html"
        browseHome
        RequestErrors.NOT_FOUND "Not found!"
    ]

startWebServer config webApp
