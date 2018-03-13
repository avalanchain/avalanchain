# FROM microsoft/dotnet:runtime
# FROM microsoft/aspnetcore
# COPY /deploy /
# WORKDIR /Server
# EXPOSE 8085
# ENTRYPOINT [ "dotnet", "Server.dll" ]

FROM microsoft/aspnetcore
COPY /deploy .
WORKDIR .
EXPOSE 8085
ENTRYPOINT ["dotnet", "Server.dll"]