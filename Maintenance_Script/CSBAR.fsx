// Include CsbScaffold
#load @".paket\load\main.group.fsx"

open FSharp.Data.Sql
open System
open FSharp.Plotly
open FSharp.Plotly.Axis
open FSharp.Data.Sql.Providers
open MySql

let axis title = LinearAxis.init(Title=title,Mirror=StyleParam.Mirror.All,Ticks=StyleParam.TickOptions.Inside,Showgrid=true,Showline=true)
let axisRange title (min,max) showtick = LinearAxis.init(Title=title,Range=StyleParam.Range.MinMax (min,max),Mirror=StyleParam.Mirror.All,Ticks=StyleParam.TickOptions.Inside,Showgrid=true,Showline=true,Showticklabels=showtick)


///mail settings
let sender = "" // ConfigurationManager.AppSettings.["mailsender"]
let password = "" // ConfigurationManager.AppSettings.["mailpassword"] |> my-decrypt
let server = "" // ConfigurationManager.AppSettings.["mailserver"]
let port = 587 //465 
let room = "23-113"
let personInCharge = "Kevin or Venny"
let dataBaseInstallationDate = System.DateTime(2018,08,23)

///defined existing directory on your local machine
let storageFolder = @"C:\Users\bvenn\Pictures\tmpCSBar\"


///database server settings
//Change the password in the connection string to the password you set in docker-compose.yml
[<Literal>]
let ConnectionStr = @"Server=127.0.0.1; Port=42333; Database=CSBarDB; uid=root; pwd=pleasechangeme"
type Sql = SqlDataProvider<Common.DatabaseProviderTypes.MYSQL,ConnectionStr,UseOptionTypes=true>
type DbContext = Sql.dataContext

type Departments =
|Bock
|Stitt
|Willmitzer 
|TestDepartment

let stringOfDepartment (d:Departments) =
    match d with
    | Bock      -> "Bock"
    | Stitt -> "Stitt"
    | Willmitzer -> "Willmitzer"
    | TestDepartment -> "TestDepartment"

let stringToDepartments (str:string) =
    match str with
    | "Bock"        -> Bock
    | "Stitt"    -> Stitt
    | "Willmitzer"   -> Willmitzer
    | "TestDepartment" -> TestDepartment
    | _ -> failwithf "%s is not a known department" str

[<AutoOpen>]
module JaggedArray =
    let transpose (arr: 'T [][]) =
        if arr.Length > 0 then 
            let colSize = arr.[0].Length
            Array.init (colSize) (fun rowI ->  Array.init (arr.Length) (fun colI -> (arr.[colI].[rowI])))
        else
            arr

[<AutoOpen>]
module Person =

    type Person = {
        Id: int64
        Name: string
        Pin:int
        Email: string
        Status: byte
    }
    
    type PersonEntity = DbContext.``CSBarDB.PersonEntity``

    let mapToPerson (p:PersonEntity) =
        {
            Id = p.Id
            Name = p.Name
            Pin = p.Pin
            Email = p.Email
            Status = p.Status
        }

    let getAllPersons () =
        let ctx = Sql.GetDataContext()
        query {
            for p in ctx.CsBarDb .Person do
            select p
        }
        |> Seq.map mapToPerson 

    let getPersonByPin (pin:int) =
        let ctx = Sql.GetDataContext()
        query {
            for p in ctx.CsBarDb.Person do
            where (p.Pin = pin)
            select (Some p)
            exactlyOneOrDefault
        }

    let getPersonByName (name:string) =
        let ctx = Sql.GetDataContext()
        query {
            for p in ctx.CsBarDb.Person do
            where (p.Name = name)
            select (Some p)
            exactlyOneOrDefault
        }

    let getPersonByEmail (email:string) = 
        let ctx = Sql.GetDataContext()
        query {
            for p in ctx.CsBarDb.Person do
            where (p.Email = email)
            select (Some p)
            exactlyOneOrDefault
        }

    let getPersonById (id: int64) = 
        let ctx = Sql.GetDataContext()
        query {
            for p in ctx.CsBarDb.Person do
            where (p.Id = id)
            select (Some p)
            exactlyOneOrDefault
        }

    let getUsedPins () =
        let ctx = Sql.GetDataContext()
        ctx.CsBarDb.Person
        |> Seq.map mapToPerson
        |> Array.ofSeq
        |> Array.map (fun p -> p.Pin)


    let createNewPin () =
        let usedPins = getUsedPins ()
        let rnd = new System.Random()
        let rec loop () = 
            let r = rnd.Next(1000,9999)
            if not (Array.contains r usedPins) then
                r
            else loop ()
        loop ()

    let createNewUser (name:string) (email:string) =
        let pin = createNewPin()
        {
            Id = -1L
            Name = name
            Pin = pin
            Email = email
            Status = byte 1
        }

    let insertPerson (p:Person) =
        let ctx = Sql.GetDataContext()
        let t = ctx.CsBarDb.Person.``Create(Email, Name, Pin, Status)``(p.Email,p.Name,p.Pin,p.Status)
        ctx.SubmitUpdates()

[<AutoOpen>]
module Trade = 
    type Trade = {
        Id: int64
        Name: string
        Price: float
        Category: string
        ExtId: string
    }

    let insertTrade (trade:Trade) = 
        let ctx = Sql.GetDataContext()
        let t = ctx.CsBarDb.Trade.``Create(Category, ExtId, Name, Price)``(trade.Category,trade.ExtId,trade.Name,trade.Price)
        ctx.SubmitUpdates()
        
    let getTradeByName (name:string) =
        let ctx = Sql.GetDataContext()
        query {
            for p in ctx.CsBarDb.Trade do
            where (p.Name = name)
            select (Some p)
            exactlyOneOrDefault
        }

    let changePrice name price=
        let ctx = Sql.GetDataContext()
        query {
            for p in ctx.CsBarDb.Trade do
            where (p.Name = name)
            select (Some p)
            exactlyOneOrDefault
        }
        |> fun x -> printfn "old price: %.2f" x.Value.Price
                    x.Value.Price <- price
                    printfn "new price: %.2f" x.Value.Price
        ctx.SubmitUpdates()

    //creates a new trade with same properties as the old one but with updated price. Old Trade is deactivated by altering name and barcode
    let updatePrice name price =
        let ctx = Sql.GetDataContext()
    
        //change old barcode
        let oldTrade = 
            query {
                for p in ctx.CsBarDb.Trade do
                where (p.Name = name)
                select (Some p)
                exactlyOneOrDefault
            }

        let barcode = oldTrade.Value.ExtId

        oldTrade
        |> fun x -> printfn "old Price: %.2f \n ext: %s" x.Value.Price x.Value.ExtId
                    x.Value.Name <- name + "x"
                    x.Value.ExtId <- x.Value.ExtId + "x"
                    printfn "new ext: %s" x.Value.ExtId
        ctx.SubmitUpdates()
        {
            Id      = 2L                        
            Name    = name
            Price   = price            
            Category= oldTrade.Value.Category       
            ExtId   = barcode  
        } 
        |> insertTrade 

    let changeName nameOld nameNew=
        let ctx = Sql.GetDataContext()
        query {
            for p in ctx.CsBarDb.Trade do
            where (p.Name = nameOld)
            select (Some p)
            exactlyOneOrDefault
        }
        |> fun x -> printfn "old name: %s" x.Value.Name
                    x.Value.Name <- nameNew
                    printfn "new name: %s" x.Value.Name
        ctx.SubmitUpdates()

    let getTradeByExtId (extid:string) = 
        let ctx = Sql.GetDataContext()
        query {
            for p in ctx.CsBarDb.Trade do
            where (p.ExtId = extid)
            select (Some p)
            exactlyOneOrDefault
        }

    let getTradeById (id: int64) = 
        let ctx = Sql.GetDataContext()
        query {
            for p in ctx.CsBarDb.Trade do
            where (p.Id = id)
            select (Some p)
            exactlyOneOrDefault
        }

[<AutoOpen>]
module Orders = 

    type Orders = {
        FK_Person: int64
        FK_Trade: int64
        Amount:int
        Time: System.DateTime
    }

    type OrdersEntity = DbContext.``CSBarDB.OrdersEntity``

    let createOrder fkp fkt amt =
        {
            FK_Person = fkp
            FK_Trade = fkt
            Amount = amt
            Time = System.DateTime.Now
        }

    let mapToOrders (o: OrdersEntity) =
        {
            FK_Person = o.FkPerson
            FK_Trade = o.FkTrade
            Amount = o.Amount
            Time = o.Time
        }

    let getAllOrders() = 
        let ctx = Sql.GetDataContext()
        query {
            for o in ctx.CsBarDb.Orders do
            select o
        }
        |> Seq.map mapToOrders

    let insertOrder (o:Orders) =
        let ctx = Sql.GetDataContext()
        let t = ctx.CsBarDb.Orders.Create()
        t.FkPerson<-o.FK_Person
        t.FkTrade<-o.FK_Trade
        t.Amount<-o.Amount
        t.Time<- System.DateTime.Now
        ctx.SubmitUpdates()

[<AutoOpen>]
module PersonInfo =

    type PersonInfos = {
        Attribute : string
        FK_Person: int64
        Value: option<string>
    }

    let createPersonInfos attr fkp value = {Attribute=attr;FK_Person=fkp;Value=value}

    type PersonInfosEntity = DbContext.``CSBarDB.PersonInfosEntity``
    
    let mapToPersonInfos (pi : PersonInfosEntity) =
        {
            Attribute = pi.Attribute
            FK_Person = pi.FkPerson 
            Value = pi.Value
        }
    
    let insertPersonInfos (p: PersonInfos) =
        let ctx = Sql.GetDataContext()
        let t = ctx.CsBarDb.PersonInfos.``Create(Attribute, FK_Person)``(p.Attribute,p.FK_Person)
        t.Value <- p.Value
        ctx.SubmitUpdates()

[<AutoOpen>]
module Components =

    type OrderInfoEntity = DbContext.``CSBarDB.OrderInfoEntity``

    type OrderInfo = {
        Amount: int
        Department: Departments
        Category: string
        Price : float
        Time: System.DateTime
        TradeName: string
        PersonName:string
    }

    let mapToOrderInfo (o: OrderInfoEntity) =
        let dep =
            if o.Attribute = "AG" then
                stringToDepartments o.Value.Value
            else failwith "Attribute unknown"
        {
            Amount = o.Amount
            Department = dep
            Category = o.Category
            Price = o.Price
            Time = o.Time
            TradeName = o.Name
            PersonName = o.PersonName
        }
    let getAllOrderInfos () =
        let ctx = Sql.GetDataContext()
        query {
            for oi in ctx.CsBarDb.OrderInfo do
            select oi
        }
        |> Seq.map mapToOrderInfo

    let getBalance (personId: int64) =
        let ctx = Sql.GetDataContext()
        query {
            for b in ctx.CsBarDb.CurrentBalance do
            where (b.FkPerson = personId)
            select (Some b)
            exactlyOneOrDefault
        }

    let createNewCSBarUser (name: string) (email: string) (department:Departments) =
        let person = createNewUser name email
        person
        |> insertPerson 
        let id = 
            match (getPersonByName name) with
            |Some p -> p.Id
            |_ -> failwith "Something went wrong. The Id of the now inserterted person cannot be found."
        printfn "%i" id
        let infos = createPersonInfos "AG" id (Some (stringOfDepartment department))
        
        //insert first order to initialize balance
        let checkBalance = 
            match (getTradeByExtId "7610095003003") with
            |Some d -> d
            |_ -> failwith "ExtID 7610095003003 not found"

        infos
        |> insertPersonInfos
        {
            FK_Person = id
            FK_Trade = checkBalance.Id
            Amount = 1
            Time = System.DateTime.Now
        }
        |> insertOrder

    let getBalanceByUserName (name: string) = 
        let person = 
            let p' = getPersonByName name
            match p' with
            | Some p -> p |> mapToPerson
            | None -> failwithf "person %s not found in database" name
        let balance = getBalance person.Id
        match balance with
        |Some b -> b.Balance
        |_ -> failwith "User has no balance. Tick one order or scan the checkBalance barcode to initialize the balance."

    let deposit (personName:string) (amount:int)=
        let balanceBefore = getBalanceByUserName personName
        match balanceBefore with
        |Some b -> printfn "old balance for %s: %.2f" personName b
        |_ -> failwith "User has no balance. Tick one order or scan the checkBalance barcode to initialize the balance."
        
        let deposit = 
            match (getTradeByExtId "cace0f69-1748-439c-a2e9-58449a6af8fb") with
            |Some d -> d
            |_ -> failwith "Deposit not found"

        let p = 
            match (getPersonByName personName) with
            |Some p -> p
            |_ -> failwithf "Person %s not found" personName
        
        {
            FK_Person = p.Id
            FK_Trade = deposit.Id
            Amount = amount
            Time = System.DateTime.Now
        }
        |> insertOrder

        let newBalance = getBalanceByUserName personName
        match newBalance with
        |Some b -> printfn "new balance for %s: %.2f. \n%.2f was deposited" personName b (abs (balanceBefore.Value - b))
        |_ -> failwith "Fetching balance failed - this should not happen and you should be concerned lol" 
    
    let debit (personName:string) (amount:int)=
        let balanceBefore = getBalanceByUserName personName
        match balanceBefore with
        |Some b -> printfn "old balance for %s : %.2f" personName b
        |_ -> failwith "User has no balance. Tick one order or scan the checkBalance barcode to initialize the balance."
        
        let debit = 
            match (getTradeByExtId "a0b11e0b-4dcb-4a06-a7c3-77e0fb0a23da") with
            |Some d -> d
            |_ -> failwith "Debit not found"
    
        let p = 
            match (getPersonByName personName) with
            |Some p -> p
            |_ -> failwithf "person %s not found" personName
        
        {
            FK_Person = p.Id
            FK_Trade = debit.Id
            Amount = amount
            Time = System.DateTime.Now
        }
        |> insertOrder
    
        let newBalance = getBalanceByUserName personName
        match newBalance with
        |Some b -> printfn "new balance for %s: %.2f. \n%.2f was deposited" personName b (abs (balanceBefore.Value - b))
        |_ -> failwith "Fetching balance failed - this schould not happen and you should be concerned lol" 

    let tick (personName:string) (tradename:string) (amount:int)=
        let deposit = 
            match (getTradeByName tradename) with
            |Some d -> d
            |_ -> failwith "Trade not found"
        let p = 
            match (getPersonByName personName) with
            |Some p -> p
            |_ -> failwithf "Person %s not found" personName      
        {
            FK_Person = p.Id
            FK_Trade = deposit.Id
            Amount = amount
            Time = System.DateTime.Now
        }
        |> insertOrder

    let createTrade name price category barcode = 
        {
        Id      = 2L
                    /////////////////////                         
        Name    = name
        Price   = price  
        Category= category     
        ExtId   = barcode  
        } 
        |> insertTrade

[<AutoOpen>]
module Mail =

    open System
    open System.Net.Mail

    let sendMailMessage email name topic msg (ccAdresses :string[]) (attachmentPaths :string[]) =
        use msg = 
            new MailMessage(
                sender, email, topic, "Dear " + name + ", <br/><br/>\r\n\r\n" + msg)
        (
            msg.IsBodyHtml <- true
            let atts = attachmentPaths |> Array.map (fun attachment -> new Attachment(attachment))
            let addAtts = atts |> Array.map (fun att -> msg.Attachments.Add(att))
    
            let ccs = ccAdresses |> Array.map (fun x-> msg.CC.Add(x))
    
            use client = new SmtpClient(server, port)
            (
                client.EnableSsl <- true
                client.Credentials <- System.Net.NetworkCredential(sender, password)
                client.SendCompleted |> Observable.add(fun e -> 
                    let msg = e.UserState :?> MailMessage
                    if e.Cancelled then
                        ("Mail message cancelled:\r\n" + msg.Subject) |> Console.WriteLine
                    if e.Error <> null then
                        ("Sending mail failed for message:\r\n" + msg.Subject + 
                            ", reason:\r\n" + e.Error.ToString()) |> Console.WriteLine
                    if msg<>Unchecked.defaultof<MailMessage> then msg.Dispose()
                    if client<>Unchecked.defaultof<SmtpClient> then client.Dispose()
                )
                // Maybe some System.Threading.Thread.Sleep to prevent mail-server hammering
                System.Threading.Thread.Sleep(1000)
                client.Send(msg)
            )
        )
        
[<AutoOpen>]
module MessageTemplates =

    let schuldenEintreiber (p:Person) (balance: float) (*plot*) =
        let body = 
            [
                sprintf "The current balance of your <b>CSBar</b> account is: <b>%.2f</b>" -balance
                sprintf "You can charge your credit in room %s. Please search for %s." room personInCharge
                "Deposits can be made in multiple of 5 (preferably in banknotes). Do not worry about overpaying. Positive balances will always be carried over to the next month(s) and cashback is guaranteed should you decide to leave the ticker system."
                
                "<b>All the best</b>,"
                "your <b>CSBar</b>-Team<br>"
                "<hr>"
                "<i>Additional note: This mail adress is planned to be used for automated messages and information about the CSBar ticking system. Consider whitelisting it.</i>"
                //sprintf "<img src=\"data:image/jpg;base64,%s\" />" plot
            ]
            |> String.concat "<br>"
        body

    let welcomeToCSBar (p:Person) =
        let body = 
            [
                "welcome to CSBar, your automated ticking system in our break room."
                sprintf "You will be able to participate with your personal pin **<b>%04i</b>** and use it for coffee and beverages. We will send you a monthly bill to your email adress. If you have any question contact us." p.Pin
                "<b>All the best</b>,"
                sprintf "your <b>CSBar</b>-Team (%s)" room
                "<hr>"
                "<i>Additional note: This mail adress is planned to be used for automated messages and information about the CSBar ticking system. Consider whitelisting it.</i>"
            ]
            |> String.concat "<br></br>\r\n"
        body

    let serverMaintenance()=
        let body = 
            [
                "because of server maintenance the CSBar system will be down for approximately one day. We will inform you when this process is finished. We trust in you, ticking your trades when the service is online again."
                "<b>All the best</b>,"
                sprintf "your <b>CSBar</b>-Team (%s)" room
            ]
            |> String.concat "<br></br>\r\n"
        body

    let sendWelcomeMail (p:Person) = sendMailMessage p.Email p.Name "CSBar **pin**" (welcomeToCSBar p) [||] [||]
     
[<AutoOpen>]
module Aux =

    ///prints all user names into the console
    let printAllPersons() = getAllPersons() |> Array.ofSeq |> Array.iter (fun x -> printfn "%s" x.Name)
    
    ///prints all pins into the console
    let printAllPersonPins() = getAllPersons() |> Array.ofSeq |> Array.iter (fun x -> printfn "%40s %i" x.Name x.Pin)

    ///dateformat: DD.MM.YYYY hh:mm
    let coffeeFromTo (date1:string) (date2:string) =
        let date1m = System.DateTime(int date1.[6..9],int date1.[3..4],int date1.[0..1],int date1.[11..12],int date1.[14..15],0)
        let date2m = System.DateTime(int date2.[6..9],int date2.[3..4],int date2.[0..1],int date2.[11..12],int date2.[14..15],0)
        getAllOrderInfos()
        |> Array.ofSeq
        |> Array.filter (fun x -> x.Time > date1m && x.Time < date2m && x.TradeName = "Coffee")
        |> Array.length

    module Single =
            open FSharp.Plotly.StyleParam
    
            let printReportByName (name:string) =
                let header = sprintf "| %-26s | %-20s| %-35s| %3s *%6s | %7s |"  "Name" "Time" "TradeName" "Amount" "Price" "Sum"
                let separator sep= [for i = 0 to header.Length - 1 do yield sep] |> String.concat ""
                let mutable overAllSum = 0.
                let mutable day = 0
                let mutable coffeeticker = 0
                let weeksSince = 
                    System.DateTime.Now - dataBaseInstallationDate
                    |> fun x -> x.TotalDays / 7. 
                printfn "\n%*s" (header.Length / 2 + 6) "Ticking report"
                printfn "%s\n%s\n%s" (separator "_") header (separator "|")

                getAllOrderInfos ()
                |> Seq.filter (fun x -> x.PersonName = name)
                |> Seq.sortBy (fun x -> x.Time)
                |> Seq.iteri  (fun i x -> 
                    let sum = (float x.Amount * x.Price)
                    let printspace = 
                        if x.Time.Day <> day && i <> 0 then 
                            day <- x.Time.Day
                            printfn "| %-26s - %-20s- %-35s- %6s  %6s - %7s |" "" "" "" "" "" ""
                    overAllSum <- overAllSum + sum
                    if x.TradeName = "Coffee" then coffeeticker <- coffeeticker + 1
                    printfn "| %26s | %-20s| %-35s| %6i *%6.2f | %7.2f |" x.PersonName (x.Time.ToString()) x.TradeName x.Amount x.Price sum)        
                printfn "%s" (separator "|")
                printfn "%s" (sprintf "|%*s %7.2f |" (header.Length-11) "Sum: " overAllSum)
                printfn "%s" (sprintf "|%*s %7i |" (header.Length-11) "Coffee (since beginnig): " coffeeticker )
                printfn "%s" (sprintf "|%*s %7.2f |" (header.Length-11) "coffee/week: " (float coffeeticker/  weeksSince))
                printfn "%s" (separator "^")

        
            let getReportByName (name:string) =
                let header = sprintf "| %-20s| %-35s| %3s *%6s | %7s |" "Time" "TradeName" "Amount" "Price" "Sum"
                let separator sep = [for i = 0 to header.Length - 1 do yield sep] |> String.concat ""
        
                let header = [|
                    sprintf "\n%*s" (header.Length / 2 + 6) "Ticking report"
                    sprintf "%s\n%s\n%s" (separator "_") header (separator "|")|]

                let body =
                    getAllOrderInfos ()
                    |> Seq.filter (fun x -> x.PersonName = name && x.Time.DayOfYear > DateTime.Now.DayOfYear - 35 )
                    |> Seq.sortBy (fun x -> x.Time)
                    |> Seq.mapi  (fun i x -> 
                        let sum = (float x.Amount * x.Price)               
                        sprintf "| %-20s| %-35s| %6i *%6.2f | %7.2f |" (x.Time.ToString()) x.TradeName x.Amount x.Price sum)   
                    |> Array.ofSeq
        
                let subscript = [| sprintf "%s" (separator "^")|]
                [|header;body;subscript|] |> Array.concat

            let plotReportByName ignoreDeposits showChart name = 
                let mutable max = 0.
                let mutable min = 0.
                let chart =
                    getAllOrderInfos ()
                    |> Seq.filter (fun x -> 
                        if ignoreDeposits then x.PersonName = name && x.TradeName <> "Deposit" && x.TradeName <> "Debit" && x.Time > dataBaseInstallationDate
                        else x.PersonName = name)
                    |> Seq.sortBy (fun x -> x.Time)
                    |> Seq.map (fun x -> x.Time,(float x.Amount * x.Price),x.TradeName)
                    |> Array.ofSeq
                    |> fun p ->
                                let x = p |> Array.unzip3 |> (fun (a,b,c) -> (a,b)) |> fun (k,l)  ->  Array.zip k l
                                let lables = p |> Array.unzip3 |> fun (a,b,c) -> c
                                for i=1 to x.Length - 1 do x.[i] <- (fst x.[i]),((snd x.[i-1]) + (snd x.[i]))
                                max <- snd (x |> Array.maxBy snd)
                                min <- snd (x |> Array.minBy snd)
                                x,lables
                    |> fun (data,lables) -> Chart.Area(data,Labels=lables)
                    |> Chart.withY_AxisStyle "debt in Euro"

                let shapes = 
                    let start = dataBaseInstallationDate
                    let rec loop (i:DateTime) acc =
                        if i < DateTime.Now.AddDays(-1.) then 
                            loop (i.AddDays(7.)) (i::acc)
                        else acc |> List.rev
                    loop start []
                    |> List.map (fun x -> Shape.init (StyleParam.ShapeType.Rectangle,x,x.AddDays(2.),min,max,Line=Line.init(Color="#f2a0a0"),Opacity=0.3,Fillcolor="#f2a0a0"))
                chart |> Chart.withShapes(shapes) |> Chart.withTraceName name
                |> Chart.withTitle (sprintf "debt time course of %s" name)
                |> fun x -> 
                    if showChart then 
                        x |> Chart.withSize (1000.,700.) |> Chart.Show
                        x
                    else x
            
            let plotReportByNameAndPin ignoreDeposits showChart name pin =
                let pin = (getPersonByName(name).Value|> mapToPerson).Pin = pin
                if pin then 
                    plotReportByName ignoreDeposits showChart name
                else 
                    failwithf "\nGiven PIN does not match!\n"
    
            let plotWeekDistByName showChart name =
                
                let yAxis() =LinearAxis.init(Title="#count",Mirror=Mirror.All,Ticks=TickOptions.Inside,Showgrid=false,Showline=true)
                let xAxis() =LinearAxis.init(Range=Range.MinMax (-4000000.,83000000.),Title="",Mirror=Mirror.All,Ticks=TickOptions.Inside,Showgrid=false,Showline=true)
                let nameSeq =
                    if name = "" then 
                        getAllOrderInfos()
                    else 
                        getAllOrderInfos()
                        |> Seq.filter (fun x -> x.PersonName = name)
                let coffee =
                    nameSeq
                    |> Seq.filter (fun x-> x.TradeName = "Coffee")
                    |> Seq.map (fun x -> DateTime(1970,1,1,x.Time.Hour,x.Time.Minute,0))
                    |> fun x -> Chart.Histogram(x,nBinsx=24,Color="#4b77ad") |> Chart.withX_Axis(xAxis()) |> Chart.withY_Axis(yAxis())  |> Chart.withTraceName "coffee"
                let bev =
                    nameSeq
                    |> Seq.filter (fun x-> x.Category = "Beverage")
                    |> Seq.map (fun x -> DateTime(1970,1,1,x.Time.Hour,x.Time.Minute,0))
                    |> fun x -> Chart.Histogram(x,nBinsx=24,Color="#ad504b") |> Chart.withX_Axis(xAxis()) |> Chart.withY_Axis(yAxis())|> Chart.withTraceName "beverage"
                let beer =
                    nameSeq
                    |> Seq.filter (fun x-> x.Category <> "Beverage" && x.Category <> "Coffee" && x.Category <> "Deposit" && x.Category <> "Debit" && x.Category <> "TestStuff")
                    |> Seq.map (fun x -> DateTime(1970,1,1,x.Time.Hour,x.Time.Minute,0))
                    |> fun x -> Chart.Histogram(x,nBinsx=24,Color="#4bad81") |> Chart.withX_Axis(xAxis()) |> Chart.withY_Axis(yAxis())|> Chart.withTraceName "beer&other"
                [coffee;bev;beer]
                |> Chart.Stack(1)
                |> Chart.withTitle (sprintf "daily distribution %s" name)
                |> fun x -> 
                    if showChart then x |> Chart.Show
                    x

            let plotWeekDistByNameAndDay showChart name =
                let nameSeq =
                    if name = "" then 
                        getAllOrderInfos()
                        |> Seq.groupBy (fun x -> x.Time.DayOfWeek)
                        |> Seq.sortBy (fun (key,items) -> key)
                    else 
                        getAllOrderInfos()
                        |> Seq.filter (fun x -> x.PersonName = name)
                        |> Seq.groupBy (fun x -> x.Time.DayOfWeek)
                        |> Seq.sortBy (fun (key,items) -> key)
                        |> fun x -> Seq.append (Seq.tail x) (seq [Seq.head x])

                let coffee day seq showtick =
                    seq
                    |> Seq.filter (fun x-> x.TradeName = "Coffee")
                    |> Seq.map (fun x -> DateTime(1970,1,1,x.Time.Hour,x.Time.Minute,0))                                                                                                                                                                                         
                    |> (fun x -> Chart.Histogram(x,nBinsx=24,Color="#4b77ad") |> Chart.withX_Axis (axisRange (if showtick then "coffee" else "") (-4000000.,83000000.) showtick)|> Chart.withY_Axis (axis "#count") |> Chart.withTraceName (sprintf "%s coffee" day))  
                let beverage day seq showtick=                                                                                                                                                                                          
                    seq                                                                                                                                                                                                                 
                    |> Seq.filter (fun x-> x.Category = "Beverage")                                                                                                                                                                     
                    |> Seq.map (fun x -> DateTime(1970,1,1,x.Time.Hour,x.Time.Minute,0))                                                                                                                                                
                    |> (fun x -> Chart.Histogram(x,nBinsx=24,Color="#ad504b") |> Chart.withX_Axis (axisRange (if showtick then "beverage" else "") (-4000000.,83000000.) showtick)|> Chart.withY_Axis (axis "") |> Chart.withTraceName (sprintf "%s beverage" day))
                let beer day seq showtick=                                                                                                                                                                                              
                    seq                                                                                                                                                                                                                 
                    |> Seq.filter (fun x-> x.Category <> "Beverage" && x.Category <> "Coffee" && x.Category <> "Deposit" && x.Category <> "Debit" && x.Category <> "TestStuff")                                                         
                    |> Seq.map (fun x -> DateTime(1970,1,1,x.Time.Hour,x.Time.Minute,0))                                                                                                                                                
                    |> (fun x -> Chart.Histogram(x,nBinsx=24,Color="#4bad81") |> Chart.withX_Axis (axisRange (if showtick then "beer" else "") (-4000000.,83000000.) showtick)|> Chart.withY_Axis (axis "")  |> Chart.withTraceName (sprintf "%s beer" day))   

                nameSeq
                |> Seq.mapi (fun i (day,x) -> 
                    let showtick = i=6 
                    coffee (day.ToString()) x showtick ,
                    beverage (day.ToString()) x showtick ,
                    beer (day.ToString()) x showtick )
                |> Seq.unzip3
                |> fun (cof,bev,bee) ->
                    [|
                        cof |> Array.ofSeq
                        bev |> Array.ofSeq
                        bee |> Array.ofSeq
                    |]
                    |> JaggedArray.transpose
                    |> Array.concat
                    |> Chart.Stack 3
                    |> Chart.withSize (1200.,800.)
                    |> Chart.withTitle (sprintf "daily order distribution for %s" name)
                    |> Chart.withMarginSize(Left=50.,Bottom=50.,Top=50.)
                    |> fun l -> 
                        if showChart then l |> Chart.Show
                        l

            let plotWeekDistByNameAndPin name pin =
                let pin = (getPersonByName(name).Value|> mapToPerson).Pin = pin
                if pin then plotWeekDistByName true name
                else failwithf "\nGiven PIN does not match!\n"

            let printReportByNameAndPin (name:string) (pin:int)=        
                let pin = (getPersonByName(name).Value|> mapToPerson).Pin = pin
                if pin then printReportByName name
                else printfn "\nGiven PIN does not match!\n"

            let plotCoffeCourse name=
                let dayArray =
                    let rec loop i acc=
                        if i < System.DateTime.Now then
                            loop (i.AddDays(1.)) ((i,0)::acc)
                        else acc |> List.rev |> Array.ofList
                    loop dataBaseInstallationDate []

                let orders =
                    getAllOrderInfos ()
                    |> Seq.filter (fun x -> x.TradeName = "Coffee" && x.Amount = 1 && x.PersonName = name)
                    |> Seq.sortBy (fun x -> x.Time)
                    |> Seq.map (fun x -> x.Time,x.Amount)
                    |> Array.ofSeq
        
                [|dayArray;orders|] 
                |> Array.concat 
                |> Array.sortBy fst
                |> Array.groupBy (fun (date,amount) -> date.ToShortDateString()) 
                |> Array.map (fun (date,a) -> date,a |> Array.sumBy snd)
                |> Chart.Column
                |> Chart.withTitle (sprintf "CoffeCourse %s" name)
                |> fun x -> 
                    x |> Chart.Show
                    x


            let writeReportToFile (name:string) path =
                let preheader = sprintf "| %-26s | %-20s| %-35s| %3s *%6s | %7s |" "Name" "Time" "TradeName" "Amount" "Price" "Sum"
                let separator sep= [for i = 0 to preheader.Length - 1 do yield sep] |> String.concat ""
                let mutable day = 0

                let header =
                    seq [
                    sprintf "\n%*s" (preheader.Length / 2 + 6) "Ticking report"
                    sprintf "%s\r\n%s\r\n%s" (separator "_") preheader (separator "|")
                    ]

                let body =
                    getAllOrderInfos ()
                    |> Seq.filter (fun x -> x.PersonName = name)
                    |> Seq.sortBy (fun x -> x.Time)
                    |> Seq.mapi  (fun i x -> 
                        let sum = (float x.Amount * x.Price)
                        let printspace = 
                            if x.Time.Day <> day && i <> 0 then 
                                day <- x.Time.Day
                                sprintf "| %-26s - %-20s- %-35s- %6s  %6s - %7s |\r\n" "" "" "" "" "" ""
                            else ""

                        sprintf "%s| %26s | %-20s| %-35s| %6i *%6.2f | %7.2f |" printspace x.PersonName (x.Time.ToString()) x.TradeName x.Amount x.Price sum)        
        
                let suffix = 
                    seq [sprintf "%s" (separator "*");
                         sprintf "%*s" (preheader.Length / 2 + 7) "**end of report**"]

                let text = 
                    [header;
                     body;  
                     suffix;] |> Seq.concat

                System.IO.File.WriteAllLines(path,text)

            let writeReportToFileWithPin (name:string) (pin:int) (path:string)=        
                let pin = (getPersonByName(name).Value|> mapToPerson).Pin = pin
                if pin then writeReportToFile name path
                else printfn "wrong pin"

            let savePersonalStats name path =
                plotCoffeCourse name                  |> Chart.SaveHtmlAs (path + "CoffeeCourse")
                plotReportByName false false name     |> Chart.SaveHtmlAs (path + "Report")
                plotWeekDistByName false name         |> Chart.SaveHtmlAs (path + "weekDistribution")
                writeReportToFile name (path + "report.txt")
                plotWeekDistByNameAndDay false name |> Chart.SaveHtmlAs (path + "weeklyDistribution") 

            let sendReport name =
                getPersonByName name
                |> fun x -> x.Value
                |> mapToPerson
                |> fun p -> p,schuldenEintreiber p  
                |> fun (p,msgBody) -> 
                    let coursePath = storageFolder + "timeCourse_" + (p.Email.Split '@' |> Seq.head) + ".html"
                    let weekDist = storageFolder + "dailyDistribution_" + (p.Email.Split '@' |> Seq.head) + ".html"
                    plotReportByName false false p.Name |> Chart.withSize (1000.,650.) |> Chart.SaveHtmlAs (coursePath)
                    plotWeekDistByName false p.Name     |> Chart.withSize (700.,650.) |> Chart.SaveHtmlAs (weekDist)   
                    try 
                        sendMailMessage p.Email p.Name "Your current CSBar balance" (msgBody (getBalance p.Id).Value.Balance.Value) [||] [|coursePath;weekDist|]
                    with e as exn -> 
                        printfn "I failed xDD :(( because of \r\n%s" exn.Message
                    System.Threading.Thread.Sleep (1100)
                    System.IO.File.Delete (coursePath)
                    System.IO.File.Delete (weekDist)

            let sendReportVerbose name =
                getPersonByName name
                |> fun x -> x.Value
                |> mapToPerson
                |> fun p -> p,schuldenEintreiber p  
                |> fun (p,msgBody) -> 
  
                    let coursePath = storageFolder + "timeCourse_" + (p.Email.Split '@' |> Seq.head) + ".html"
                    let coursePathWODeposits =storageFolder + "timeCourseWODeposits_" + (p.Email.Split '@' |> Seq.head) + ".html"
                    let weekDist =   storageFolder + "dailyDistribution_" + (p.Email.Split '@' |> Seq.head) + ".html"
                    let weekly =   storageFolder + "weeklyDistribution_" + (p.Email.Split '@' |> Seq.head) + ".html"
                    let reportTxt =  storageFolder + "report_" + (p.Email.Split '@' |> Seq.head) + ".txt"
                    plotReportByName false false p.Name |> Chart.withSize (1000.,650.) |> Chart.SaveHtmlAs (coursePath)         
                    plotReportByName true false p.Name |> Chart.withSize (1000.,650.) |> Chart.SaveHtmlAs (coursePathWODeposits)
                    plotWeekDistByName false p.Name     |> Chart.withSize (700.,650.) |> Chart.SaveHtmlAs (weekDist)            
                    writeReportToFile p.Name reportTxt
                    plotWeekDistByNameAndDay false p.Name |> Chart.SaveHtmlAs weekly 
                    try 
                        sendMailMessage p.Email p.Name "Your current CSBar balance" (msgBody (getBalance p.Id).Value.Balance.Value) [||] [|coursePath;weekDist;reportTxt;weekly;coursePathWODeposits|]
                    with e as exn -> 
                        printfn "I failed xDD :(( because of \r\n%s" exn.Message
                    System.Threading.Thread.Sleep (1100)
                    System.IO.File.Delete coursePath
                    System.IO.File.Delete weekDist
                    System.IO.File.Delete reportTxt
                    System.IO.File.Delete coursePathWODeposits
                    System.IO.File.Delete weekly

    module All =

        let printAllBalances() =
            getAllPersons ()
            |> Array.ofSeq
            |> Array.map (fun p -> p,getBalance p.Id)
            |> Array.filter (fun (p,b) -> not b.IsNone && p.Name <> "TestUser")
            |> Array.map (fun (p,b) -> p,b.Value.Balance.Value)
            |> Array.sortByDescending (fun (p,b) -> b)
            |> Array.map (fun (p,v) ->  printfn "%26s  %6.2f  %30s" p.Name v p.Email
                                        p.Name,v)
            |> Chart.Column
            |> Chart.withSize (1200.,700.)
            |> Chart.withY_AxisStyle "debts"
            |> Chart.Show

        let printReport showNames =
            let header = sprintf "| %-26s | %-20s| %-35s| %3s *%6s | %7s |" "Name" "Time" "TradeName" "Amount" "Price" "Sum"
            let separator sep= [for i = 0 to header.Length - 1 do yield sep] |> String.concat ""
            let mutable dailySum = 0.
            let mutable overAllSum = 0.
            let mutable day = 0

            printfn "\n%*s" (header.Length / 2 + 6) "Ticking report"
            printfn "%s\n%s\n%s" (separator "_") header (separator "|")

            getAllOrderInfos ()
            |> Seq.sortBy (fun x -> x.Time)
            |> Seq.iteri (fun i x -> 
                let sum = (float x.Amount * x.Price)
                let capsName =
                    if showNames then x.PersonName
                    else "**********"
                //print a empty line if day changes
                let printspace = 
                    if x.Time.Day <> day && i <> 0 then 
                        day <- x.Time.Day
                        printfn "| %-26s - %-20s- %-35s- %6s  %6s - %7.2f |" "" "" "" "" "" (dailySum)
                        dailySum <- 0.
                dailySum <- dailySum + (max 0. sum) 
                overAllSum <- overAllSum + sum
                printfn "| %-26s | %-20s| %-35s| %6i *%6.2f | %7.2f |" capsName (x.Time.ToString()) x.TradeName x.Amount x.Price sum)        
    
            printfn "%s\n%s\n%s" (separator "|") (sprintf "|%*s %7.2f |"  (header.Length - 11) "Sum: " overAllSum) (separator "^")
        
        let printAllTrades() =
            let counter name =
                getAllOrderInfos ()
                |> Seq.filter (fun x -> x.TradeName = name)
                |> Seq.fold (fun acc x -> if x.Category = "Coffee" && x.Amount > 5 then acc else acc + x.Amount) 0
            let ctx = Sql.GetDataContext()
            query {
                for p in ctx.CsBarDb.Trade do        
                select (Some p)     
            }
            |> Seq.iter (fun x -> printfn "%-50s%10.2f%25s   Amount: %4i" x.Value.Name x.Value.Price x.Value.Category (counter x.Value.Name))

        let plotAllTrades() =
            let counter name =
                getAllOrderInfos ()
                |> Seq.filter (fun x -> x.TradeName = name)
                |> Seq.fold (fun acc x -> if x.Category = "Coffee" && x.Amount > 5 then acc else acc + x.Amount) 0
            let ctx = Sql.GetDataContext()
            query {
                for p in ctx.CsBarDb.Trade do        
                select (Some p)     
            }
            |> Seq.sortBy (fun x -> x.Value.Category)
            |> Seq.map (fun x -> x.Value.Name,(counter x.Value.Name),x.Value.Price)
            |> Seq.sortBy (fun (a,b,c) -> b)
            |> Seq.unzip3
            |> fun (name,amount,price) -> Chart.Column ((Seq.zip name amount),Labels = price)
            |> Chart.withY_Axis(Axis.LinearAxis.init(StyleParam.AxisType.Log,Title="consumed amount since beginning"))
            |> Chart.withSize(1200.,700.)
            |> Chart.Show
        
        let printTradeAmount name=
            let filteredData =
                getAllOrderInfos ()
                |> Seq.filter (fun x -> x.TradeName = name)
                |> Seq.sortBy (fun x -> x.Time)        
            filteredData |> Seq.iteri (fun i x -> printfn "| %-26s | %-20s| %-35s| %6i *%6.2f |" x.PersonName (x.Time.ToString()) x.TradeName x.Amount x.Price)
            printfn "Sum: %i" (filteredData |> Seq.fold (fun acc x -> acc + x.Amount) 0)
        
        let printUmsatz() =
            getAllOrderInfos ()
            |> Seq.filter (fun x-> x.Time >= dataBaseInstallationDate && x.Price > 0.)
            |> Seq.fold (fun acc x -> acc + ((float x.Amount) * x.Price)) 0.
            |> fun x -> 
                let weeksSince = float (System.DateTime.Now - dataBaseInstallationDate).Days /7.
                printfn "Umsatz seit %A: %.2f\nUmsatz pro Woche: %.2f" dataBaseInstallationDate x (x/weeksSince)
    
        let plotCSBarDebtCourse includeDeposits =
            let mutable min = 0.
            let mutable max = 0.
            let chart =
                let data=
                    getAllOrderInfos ()
                    |> Seq.groupBy (fun x-> x.Time.Date)
                    |> Seq.sortBy fst
                data
                |> Seq.map (fun (date,day) -> 
                    if includeDeposits then
                        date, day |> Seq.fold (fun acc trade -> acc + (float trade.Amount * trade.Price) ) 0. 
                    else    
                        day |> Seq.filter (fun trade -> trade.TradeName <> "Debit" && trade.TradeName <> "Deposit")
                        |> fun x -> date,x |> Seq.fold (fun acc trade -> acc + (float trade.Amount * trade.Price) ) 0. )
                |> Array.ofSeq
                |> fun x ->
                            for i=1 to x.Length - 1 do x.[i] <- (fst x.[i]),((snd x.[i-1]) + (snd x.[i]))
                            max <- x |> Array.maxBy snd |> snd
                            min <- x |> Array.minBy snd |> snd
                            x
                |> Chart.Area
                |> Chart.withY_AxisStyle "overall csbar-debt"
            let shapes = 
                let start = dataBaseInstallationDate
                let rec loop (i:DateTime) acc =
                    if i < DateTime.Now.AddDays(-1.) then 
                        loop (i.AddDays(7.)) (i::acc)
                    else acc |> List.rev
                loop start []
                |> List.map (fun x -> Shape.init (StyleParam.ShapeType.Rectangle,x,x.AddDays(2.),min,max,Line=Line.init(Color="#f2a0a0"),Opacity=0.3,Fillcolor="#f2a0a0"))

            chart 
            |> Chart.withShapes (shapes) 
            |> Chart.withSize (1100.,600.)
            |> Chart.Show
            chart 
            |> Chart.withShapes (shapes) 
            |> Chart.withSize (1100.,600.)
        
        let plotSingledayBehavior price =
        
            let data=
                getAllOrderInfos ()
                |> Seq.filter (fun x -> x.TradeName <> "Deposit" && x.TradeName <> "Debit")
                |> Seq.groupBy (fun x-> x.Time.Date)
                |> fun x -> 
                    let emptyOrders = seq [for i = 0 to (System.DateTime.Now - dataBaseInstallationDate).Days do yield (dataBaseInstallationDate.AddDays(float i), (seq [] :seq<OrderInfo>))]
                    [|emptyOrders;x|]
                |> Seq.concat 
                |> Seq.sortBy fst
                |> Array.ofSeq              
                |> Seq.skip 7
            let calc (data: seq<DateTime * seq<OrderInfo>>)=  
                let post dat= 
                    dat
                    |> Array.ofSeq
                    |> fun g -> g.[6..] 
                    |> Chart.StackedColumn
                if price then
                    data
                    |> Seq.map (fun (date,day) -> date.ToShortDateString(), day |> Seq.fold (fun acc trade -> acc + (float trade.Amount * trade.Price) ) 0. ) |> post
                else
                    data
                    |> Seq.map (fun (date,day) -> date.ToShortDateString(), day |> Seq.fold (fun acc trade -> acc + (float trade.Amount) ) 0. ) |> post       
            let coffee =
                data
                |> Seq.map (fun (date,day) -> date,day |> Seq.filter (fun trade -> trade.Price > 0. && trade.Category = "Coffee"))
                |> calc
                |> Chart.withTraceName "coffee"
            let beer =
                data
                |> Seq.map (fun (date,day) -> date,day |> Seq.filter (fun trade -> trade.Price > 0. && trade.Category <> "Coffee" && trade.Category <> "Beverage" && trade.Category <> "alkoholfreies Radler"))
                |> calc
                |> Chart.withTraceName "beer (+alc)"
            let beverages =
                data
                |> Seq.map (fun (date,day) -> date,day |> Seq.filter (fun trade -> trade.Price > 0. && (trade.Category = "Beverage" || trade.Category = "alkoholfreies Radler" )))
                |> calc
                |> Chart.withTraceName "beverages"

            [coffee;beer;beverages]
            |> Chart.Combine
            |> fun x -> if price then 
                            x |> Chart.withY_AxisStyle "sales by day and category (Euro)"
                        else x |> Chart.withY_AxisStyle "amount by day and category"
            |> Chart.withSize(1100.,700.)
            |> Chart.Show
        
        let plotOrderCounter() =
            getAllOrderInfos()
            |> Seq.groupBy (fun x-> x.PersonName)
            |> Seq.map (fun (name,orders) -> name,Seq.length orders)
            |> Seq.sortByDescending (fun (name,orderNumber) -> orderNumber)
            |> Chart.Column
            |> Chart.withSize(1200.,650.)
            |> Chart.withY_AxisStyle "#orders"
            |> Chart.Show
        
        let plotPriceCounter() =
            getAllOrderInfos()
            |> Seq.groupBy (fun x-> x.PersonName)
            |> Seq.map (fun (name,orders) -> 
                let price =
                    orders 
                    |> Seq.filter (fun x -> x.Price > 0. && x.Amount < 3)
                    |> Seq.fold (fun acc x -> acc + (x.Price * (float x.Amount))) 0.
                name,price)
            |> Seq.sortByDescending (fun (name,orderNumber) -> orderNumber)
            |> Chart.Column
            |> Chart.withSize(1200.,650.)
            |> Chart.withY_AxisStyle "overall Amount (Euro)"
            |> Chart.Show

        let plotWeekDist() =
            Single.plotWeekDistByName true ""



//==================================================================================================================
//==================================================================================================================
//In the following the functions are called. Please uncomment the respective line.

//================================================
///Charge credit==================================
//================================================

///query the current balance
//getBalanceByUserName "TestUser"

///load the balance with 10 �
//deposit "TestUser" 10

///charge 5� of user balance
//debit "TestUser" 5

//================================================
///Single user functionalities====================
//================================================

///get person infos
//getPersonByName "TestUser"

///print the user�s report into the console
//Single.printReportByName "TestUser"

///plot the report as interactive chart
//Single.plotReportByName false true "TestUser"

///plot the report as interactive chart (ignore deposits)
//Single.plotReportByName true true "TestUser"

///plot Histograms of daily consumption for coffee, beer, and beverages
//Single.plotWeekDistByName true "TestUser"

///plot Histograms of daily consumption separated by day of week
//Single.plotWeekDistByNameAndDay true "TestUser"

///sends current balance and report to user
//Single.sendReport "TestUser"

///sends verbose report to user
//Single.sendReportVerbose "TestUser"

//================================================
///All user functionalities=======================
//================================================

///print the complete report into the console
//All.printReport true

///print functions for some tables
//printAllPersons()
//All.printAllBalances()
//All.printAllTrades()

///plots daily consumption separated by category (order number)
//All.plotSingledayBehavior false

///plots daily consumption separated by category (price)
//All.plotSingledayBehavior true

///plots overall CSBar balance course
//All.plotCSBarDebtCourse true

///plots overall CSBar balance course (deposits are ignored)
//All.plotCSBarDebtCourse false

///plot Histograms of daily consumption for coffee, beer, and beverages for all users
//All.plotWeekDist()

///Special
//[
//All.plotCSBarDebtCourse true
//Single.plotReportByName false false "Benedikt Venn"
//Single.plotReportByName false false "Kevin Schneider"
//]
//|> Chart.Combine
//|> Chart.withSize(1200.,650.)
//|> Chart.withTitle ""
//|> Chart.Show

///Send balance message to all users with negative balance
//getAllPersons ()
//|> Array.ofSeq
//|> Array.map (fun p -> p,getBalance p.Id)
//|> Array.filter (fun (p,b) -> not b.IsNone)
//|> Array.map (fun (p,b) -> p,b.Value.Balance.Value)
//|> Array.filter (fun (_,b) -> b>1.)
//|> Array.sortByDescending (fun (p,b) -> b)
//|> Array.map (fun (person,balance) -> Single.sendReport person.Name)

//================================================
///Create functions===============================
//================================================

///create new user with name, email address, and associated department (from 
//createNewCSBarUser "Test User" "test@user.com" Departments.Bock 
//createNewCSBarUser "KG" "test@user.com" Departments.Stitt 
//createNewCSBarUser "KP" "test@user.com" Departments.Willmitzer
//createNewCSBarUser "lala" "b.venn@gmx.de" Departments.Bock

///update coffee price to 50 cent
//updatePrice "Coffee" 0.5

///get user information
//getPersonByName "Kw"

///send welcome mail with pin
//getPersonByName "lala"
//|> fun x -> x.Value
//|> mapToPerson
//|> sendWelcomeMail

////inserts a new trade (Id can be ignored and kept as 2L; Category= Coffee, Beer, Beverage; ExtId is the barcode as string
////         ///////////////////// Price Category Barcode
//createTrade "BitburgerRadler" 1. "Beer" "4002030456"


///get trade information
//getTradeByName "Club-Mate IceT 0.5l"


//================================================
///Create entries=================================
//================================================

///tick a order
//tick "TestUser" "Coffee" 1

///Change name of trade
//changeName  "lub-Mate 0.5l" "Club-Mate 0.5l"

////change user pin
//let test = 
//    let newPin = 999999      
//    getUsedPins() 
//    |> Array.tryFind (fun x -> x = newPin)
//    |> fun case -> if case.IsSome then failwithf "pin already used"
                
//    let ctx = Sql.GetDataContext()
//    let k =
//        query {
//            for p in ctx.Dbo.Person do
//            where (p.Name = "Test User")
//            select (Some p)
//            exactlyOneOrDefault
//        }
//    k.Value.Pin <- newPin
//    //k.Value.Email <- "test@userkl.de" 
//    ctx.SubmitUpdates()

    

