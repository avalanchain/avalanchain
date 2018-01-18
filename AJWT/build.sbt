name := "Avalanchain Jwt"

organization  := "Avalanchain"

version := "0.0.1"

scalaVersion := "2.11.8"

val circeVersion = "0.6.1"

scalacOptions := Seq("-unchecked", "-deprecation", "-encoding", "utf8")

resolvers += "Eventuate Releases" at "https://dl.bintray.com/rbmhtechnology/maven"

libraryDependencies ++= {
  val akkaVersion = "2.5.8"
  val akkaHttpVersion = "10.0.11"
  val eventuateVersion = "0.8.1"

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
    "com.typesafe.akka" %% "akka-distributed-data"                      % akkaVersion,
    "com.typesafe.akka" %% "akka-typed"                                 % akkaVersion,
    "com.typesafe.akka" %% "akka-persistence-query"                     % akkaVersion,

    "com.typesafe.akka" %% "akka-http-core"                             % akkaHttpVersion,
    "com.typesafe.akka" %% "akka-http"                                  % akkaHttpVersion,
    "com.typesafe.akka" %% "akka-http-jackson"                          % akkaHttpVersion,
    "com.typesafe.akka" %% "akka-http-spray-json"                       % akkaHttpVersion,
    "com.typesafe.akka" %% "akka-http-xml"                              % akkaHttpVersion,
    "com.typesafe.akka" %% "akka-http-testkit"                          % akkaHttpVersion,

    "org.iq80.leveldb"            % "leveldb"                           % "0.7",
    "org.fusesource.leveldbjni"   % "leveldbjni-all"                    % "1.8",

    "com.github.dnvriend" %% "akka-persistence-inmemory"                % "1.3.14",
    "com.github.dnvriend" %% "akka-persistence-journal-writer"          % "0.0.2",

    "org.scalatest"     %% "scalatest"                                  % "2.2.5" % "test",
    "ch.qos.logback"    % "logback-classic"                             % "1.1.3",
    "io.swagger"        % "swagger-core"                                % "1.5.10",
    "com.github.swagger-akka-http" %% "swagger-akka-http"               % "0.11.0",

    "org.typelevel"     %% "cats"                                       % "0.8.1",

    "org.bouncycastle"  % "bcpkix-jdk15on"                              % "1.55",
    "com.pauldijou"     %% "jwt-circe"                                  % "0.9.0",
    "io.circe"          %% "circe-core"                                 % circeVersion,
    "io.circe"          %% "circe-generic"                              % circeVersion,
    "io.circe"          %% "circe-parser"                               % circeVersion,
    "io.circe"          %% "circe-java8"                                % circeVersion,

    "com.fasterxml.jackson.core"   %  "jackson-databind"                % "2.9.0",
    "com.fasterxml.jackson.module" %% "jackson-module-scala"            % "2.9.0",

    "de.heikoseeberger" %% "akka-http-circe"                            % "1.11.0-M4",

    "com.nrinaudo"      %% "kantan.csv-cats"                            % "0.1.15",
    "org.scalaj"        %% "scalaj-http"                                % "2.3.0",

    "com.yahoofinance-api" % "YahooFinanceAPI"                          % "3.12.3",

    "com.rbmhtechnology" %% "eventuate-core"                            % eventuateVersion,
    "com.rbmhtechnology" %% "eventuate-crdt"                            % eventuateVersion,
    "com.rbmhtechnology" %% "eventuate-log-leveldb"                     % eventuateVersion,
    "com.rbmhtechnology" %% "eventuate-log-cassandra"                   % eventuateVersion,
    "com.rbmhtechnology" %% "eventuate-adapter-stream"                  % eventuateVersion


  )
}

Revolver.settings
