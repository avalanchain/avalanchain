name := "Avalanchain Jwt"

organization  := "Avalanchain"

version := "0.0.1"

scalaVersion := "2.11.8"

val circeVersion = "0.5.1"

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

    "org.scalatest"     %% "scalatest"                                  % "2.2.5" % "test",
    "com.github.swagger-akka-http" %% "swagger-akka-http"               % "0.7.2",

    "org.bouncycastle"  % "bcpkix-jdk15on"                              % "1.55",
    "com.pauldijou"     %% "jwt-circe"                                  % "0.9.0",
    "io.circe"          %% "circe-core"                                 % circeVersion,
    "io.circe"          %% "circe-generic"                              % circeVersion,
    "io.circe"          %% "circe-parser"                               % circeVersion,

    "com.nrinaudo"      %% "kantan.csv-cats"                            % "0.1.15",
    "org.scalaj"        %% "scalaj-http"                                % "2.3.0"

  )
}

Revolver.settings
