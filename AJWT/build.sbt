name := "Avalanchain Jwt"

organization  := "Avalanchain"

version := "0.0.1"

scalaVersion := "2.11.8"

val circeVersion = "0.6.1"

scalacOptions := Seq("-unchecked", "-deprecation", "-encoding", "utf8")

libraryDependencies ++= {
  val akkaVersion = "2.4.12"
  val akkaHttpVersion = "2.4.11"

  Seq(
    "com.typesafe.akka" %% "akka-actor"                                 % akkaVersion,
    "com.typesafe.akka" %% "akka-agent"                                 % akkaVersion,
    "com.typesafe.akka" %% "akka-camel"                                 % akkaVersion,
    "com.typesafe.akka" %% "akka-cluster"                               % akkaVersion,
    "com.typesafe.akka" %% "akka-cluster-metrics"                       % akkaVersion,
    "com.typesafe.akka" %% "akka-cluster-sharding"                      % akkaVersion,
    "com.typesafe.akka" %% "akka-cluster-tools"                         % akkaVersion,
    "com.typesafe.akka" %% "akka-contrib"                               % akkaVersion,
    "com.typesafe.akka" %% "akka-multi-node-testkit"                    % akkaVersion,
    "com.typesafe.akka" %% "akka-osgi"                                  % akkaVersion,
    "com.typesafe.akka" %% "akka-persistence"                           % akkaVersion,
    "com.typesafe.akka" %% "akka-persistence-tck"                       % akkaVersion,
    "com.typesafe.akka" %% "akka-remote"                                % akkaVersion,
    "com.typesafe.akka" %% "akka-slf4j"                                 % akkaVersion,
    "com.typesafe.akka" %% "akka-stream"                                % akkaVersion,
    "com.typesafe.akka" %% "akka-stream-testkit"                        % akkaVersion,
    "com.typesafe.akka" %% "akka-testkit"                               % akkaVersion % "test",
    "com.typesafe.akka" %% "akka-distributed-data-experimental"         % akkaVersion,
    "com.typesafe.akka" %% "akka-typed-experimental"                    % akkaVersion,
    "com.typesafe.akka" %% "akka-persistence-query-experimental"        % akkaVersion,

    "com.typesafe.akka" %% "akka-http-core"                             % akkaHttpVersion,
    "com.typesafe.akka" %% "akka-http-experimental"                     % akkaHttpVersion,
    "com.typesafe.akka" %% "akka-http-jackson-experimental"             % akkaHttpVersion,
    "com.typesafe.akka" %% "akka-http-spray-json-experimental"          % akkaHttpVersion,
    "com.typesafe.akka" %% "akka-http-xml-experimental"                 % akkaHttpVersion,
    "com.typesafe.akka" %% "akka-http-testkit"                          % akkaHttpVersion,

    "org.iq80.leveldb"            % "leveldb"                           % "0.7",
    "org.fusesource.leveldbjni"   % "leveldbjni-all"                    % "1.8",

    "com.github.dnvriend" %% "akka-persistence-inmemory"                % "1.3.14",
    "com.github.dnvriend" %% "akka-persistence-journal-writer"          % "0.0.2",

    "org.scalatest"     %% "scalatest"                                  % "2.2.5" % "test",
    "io.swagger"        % "swagger-core"                                % "1.5.10",
    "com.github.swagger-akka-http" %% "swagger-akka-http"               % "0.7.3",

    "org.typelevel"     %% "cats"                                       % "0.8.1",

    "org.bouncycastle"  % "bcpkix-jdk15on"                              % "1.55",
    "com.pauldijou"     %% "jwt-circe"                                  % "0.9.0",
    "io.circe"          %% "circe-core"                                 % circeVersion,
    "io.circe"          %% "circe-generic"                              % circeVersion,
    "io.circe"          %% "circe-parser"                               % circeVersion,
    "io.circe"          %% "circe-java8"                                % circeVersion,

//    "com.fasterxml.jackson.core"   %  "jackson-databind"                % "2.8.2",
//    "com.fasterxml.jackson.module" %% "jackson-module-scala"            % "2.8.2",

    "de.heikoseeberger" %% "akka-http-circe"                            % "1.11.0-M4",

    "com.nrinaudo"      %% "kantan.csv-cats"                            % "0.1.15",
    "org.scalaj"        %% "scalaj-http"                                % "2.3.0",

    "com.yahoofinance-api" % "YahooFinanceAPI"                          % "3.2.0"

  )
}

Revolver.settings
