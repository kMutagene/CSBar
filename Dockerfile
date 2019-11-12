FROM microsoft/dotnet:2.2-aspnetcore-runtime-alpine
ARG mysqlpw
COPY /deploy /
RUN echo "Server=db; Port=3306; Database=CSBarDB; uid=root ;pwd=${mysqlpw};" > ./Server/connectionstring.txt
WORKDIR /Server
RUN apk add --no-cache icu-libs
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
EXPOSE 8085
ENTRYPOINT [ "dotnet", "Server.dll" ]