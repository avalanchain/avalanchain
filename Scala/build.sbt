import com.typesafe.sbt.digest.Import._
import com.typesafe.sbt.gzip.Import._
import com.typesafe.sbt.rjs.Import._

name := """avalanchain"""

version := "1.0-SNAPSHOT"

lazy val root = (project in file(".")).enablePlugins(PlayScala, SbtWeb)

resolvers += "Sonatype Snapshots" at "https://oss.sonatype.org/content/repositories/releases/"

scalaVersion := "2.11.7"


scalacOptions := Seq(
  "-deprecation",
  "-encoding", "utf8",
  "-feature",
  "-language:higherKinds",
  "-unchecked",
  "-Xlint",
  "-Xfatal-warnings",
  "-Ywarn-dead-code",
  "-Ywarn-numeric-widen"
)


val scalaLoggingVersion = "3.1.0"
val logbackVersion = "1.1.2"
val akkaVersion = "2.4.3"
val akkaHTTPVersion = "2.4.3"
val akkaHTTPCoreVersion = "2.0.4"
val akkaPersistenceVersion = "2.4.3"
val akkaStreamVersion = "2.4.3"

val webjarsJqueryVersion = "2.1.4"
val webjarsBootswatchVersion = "3.3.5+4"
val webjarsBootstrapVersion = "3.3.6"
val webjarsReqjsVersion = "2.1.22"
val webjarsReqjsTxtVersion = "2.0.14-1"
val webjarsReactJsVersion = "0.14.3"
val webjarsJsSignalsVersion = "1.0.0"
val webjarsLoDashVersion = "3.10.1"
val scalaMetricsVersion = "3.5.2_a2.3"


val loggingScala    = "com.typesafe.scala-logging"  %% "scala-logging"                       % scalaLoggingVersion
val loggingLogback  = "ch.qos.logback"              %  "logback-classic"                     % logbackVersion
val akkaSlf4j       = "com.typesafe.akka"           %% "akka-slf4j"                          % akkaVersion
val akkaHttpCore    = "com.typesafe.akka"           %% "akka-http-core-experimental"         % akkaHTTPCoreVersion
val akkaHttp        = "com.typesafe.akka"           %% "akka-http-experimental"              % akkaHTTPVersion
val akkaStream      = "com.typesafe.akka"           %% "akka-stream"                         % akkaStreamVersion
val akkaPersistence = "com.typesafe.akka"           %% "akka-persistence"                    % akkaPersistenceVersion
val akkaPerQuery    = "com.typesafe.akka"           %% "akka-persistence-query-experimental" % akkaPersistenceVersion
val scalaMetrics    = "nl.grons"                    %% "metrics-scala"                       % scalaMetricsVersion

val webjarsJquery   = "org.webjars"                 %  "jquery"                         % webjarsJqueryVersion
val webjarsBootstrap= "org.webjars"                 %  "bootstrap"                      % webjarsBootstrapVersion
val webjarsReqjs    = "org.webjars"                 %  "requirejs"                      % webjarsReqjsVersion
val webjarsReqjsTxt = "org.webjars"                 %  "requirejs-text"                 % webjarsReqjsTxtVersion
val webjarsReactJs  = "org.webjars"                 %  "react"                          % webjarsReactJsVersion
val webjarsJsSignals= "org.webjars"                 %  "js-signals"                     % webjarsJsSignalsVersion
val webjarsLoDash   = "org.webjars"                 %  "lodash"                         % webjarsLoDashVersion

val levelDb         = "org.iq80.leveldb"            % "leveldb"                         % "0.7"
val levelDbFuse     = "org.fusesource.leveldbjni"   % "leveldbjni-all"                  % "1.8"



libraryDependencies ++= Seq(
  "org.scala-lang" % "scala-reflect" % scalaVersion.value,
  akkaStream,
  akkaPerQuery,
  loggingLogback,
  loggingScala,
  akkaSlf4j,
  akkaHttpCore,
  akkaHttp,
  akkaPersistence,
  scalaMetrics,
  webjarsBootstrap,
  webjarsJquery,
  webjarsReactJs,
  webjarsReqjs,
  webjarsReqjsTxt,
  webjarsJsSignals,
  webjarsLoDash,
  levelDb,
  levelDbFuse
)

includeFilter in(Assets, LessKeys.less) := "*.less"
excludeFilter in(Assets, LessKeys.less) := "_*.less"

pipelineStages := Seq(rjs, digest, gzip)

// Play provides two styles of routers, one expects its actions to be injected, the
// other, legacy style, accesses its actions statically.
routesGenerator := InjectedRoutesGenerator

fork in run := true