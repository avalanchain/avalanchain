name := """avalanchain"""

organization := "com.avalanchain"

version := "0.0.1"

scalaVersion := "2.11.8"

resolvers ++= Seq(
  "RoundEights" at "http://maven.spikemark.net/roundeights",
  "Sonatype Snapshots" at "https://oss.sonatype.org/content/repositories/releases/"
)

libraryDependencies ++= Seq(
  "com.typesafe.play" %% "play-netty-server" % "2.5.0",
  "org.slf4j" % "slf4j-simple" % "1.7.14",
  "com.typesafe.play" %% "play-specs2" % "2.5.0" % "test",
  
  "com.typesafe.scala-logging"  %% "scala-logging"                       % "3.1.0",
  "ch.qos.logback"              %  "logback-classic"                     % "1.1.2",
  
  "com.typesafe.akka"           %% "akka-slf4j"                          % "2.4.3",
  "com.typesafe.akka"           %% "akka-http-core-experimental"         % "2.0.4",
  "com.typesafe.akka"           %% "akka-http-experimental"              % "2.4.11",
  "com.typesafe.akka"           %% "akka-stream"                         % "2.4.11",
  "com.typesafe.akka"           %% "akka-persistence"                    % "2.4.11",
  "com.typesafe.akka"           %% "akka-persistence-query-experimental" % "2.4.11",
  "com.typesafe.akka"           %% "akka-cluster"                        % "2.4.11",
  "com.typesafe.akka"           %% "akka-cluster-metrics"                % "2.4.11",
  "com.typesafe.akka"           %% "akka-cluster-tools"                  % "2.4.11",
  "nl.grons"                    %% "metrics-scala"                       % "3.5.2_a2.3",

  //"org.scala-lang.modules"      %% "scala-pickling"                      % "0.10.2-SNAPSHOT",

  "fr.acinq"                    %% "bitcoin-lib"                         % "0.9.5",
  "com.roundeights"             %% "hasher"                              % "1.2.0",

  "org.iq80.leveldb"            % "leveldb"                              % "0.7",
  "org.fusesource.leveldbjni"   % "leveldbjni-all"                       % "1.8",

  "org.scalaz"                  %% "scalaz-core"                         % "7.2.2"

//  "org.webjars"                 %  "jquery"                         % webjarsJqueryVersion
//  "org.webjars"                 %  "bootstrap"                      % webjarsBootstrapVersion
//  "org.webjars"                 %  "requirejs"                      % webjarsReqjsVersion
//  "org.webjars"                 %  "requirejs-text"                 % webjarsReqjsTxtVersion
//  "org.webjars"                 %  "react"                          % webjarsReactJsVersion
//  "org.webjars"                 %  "js-signals"                     % webjarsJsSignalsVersion
//  "org.webjars"                 %  "lodash"                         % webjarsLoDashVersion

  
)

// META-INF discarding
assemblyMergeStrategy in assembly := {
    case PathList("META-INF", xs @ _*) => MergeStrategy.discard
    case "reference.conf" => MergeStrategy.concat
    case x =>
      val oldStrategy = (assemblyMergeStrategy in assembly).value
      oldStrategy(x)
}

fork in run := true