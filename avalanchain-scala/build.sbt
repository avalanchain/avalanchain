import java.text.SimpleDateFormat
import java.util.Date

import sbt._
import Keys._

import scala.util.Try
import scalariform.formatter.preferences._

val slf4jVersion = "1.7.21"
val logBackVersion = "1.1.7"
val scalaLoggingVersion = "3.1.0"
val slickVersion = "3.1.1"
val seleniumVersion = "2.53.0"
val circeVersion = "0.4.1"
val akkaVersion = "2.4.3"

val slf4jApi = "org.slf4j" % "slf4j-api" % slf4jVersion
val logBackClassic = "ch.qos.logback" % "logback-classic" % logBackVersion
val scalaLogging = "com.typesafe.scala-logging" %% "scala-logging" % scalaLoggingVersion
val loggingStack = Seq(slf4jApi, logBackClassic, scalaLogging)

val typesafeConfig = "com.typesafe" % "config" % "1.3.0"

val circeCore = "io.circe" %% "circe-core" % circeVersion
val circeGeneric = "io.circe" %% "circe-generic" % circeVersion
val circeJawn = "io.circe" %% "circe-jawn" % circeVersion
val circeParser = "io.circe" %% "circe-parser" % circeVersion // remove?
val circe = Seq(circeCore, circeGeneric, circeParser, circeJawn)

val sprayJson = "io.spray" %%  "spray-json" % "1.3.2"
val pickling = "org.scala-lang.modules" %% "scala-pickling" % "0.10.1"

val javaxMailSun = "com.sun.mail" % "javax.mail" % "1.5.5"

val slick = "com.typesafe.slick" %% "slick" % slickVersion
val slickHikari = "com.typesafe.slick" %% "slick-hikaricp" % slickVersion
val h2 = "com.h2database" % "h2" % "1.3.176" //watch out! 1.4.190 is beta
val postgres = "org.postgresql" % "postgresql" % "9.4.1208"
val flyway = "org.flywaydb" % "flyway-core" % "4.0"
val slickStack = Seq(slick, h2, postgres, slickHikari, flyway)
val kantan = "com.nrinaudo" %% "kantan.csv-generic" % "0.1.9"
val yahoo = "com.yahoofinance-api" % "YahooFinanceAPI" % "3.2.0"

val scalatest = "org.scalatest" %% "scalatest" % "2.2.6" % "test"
val unitTestingStack = Seq(scalatest)

val seleniumJava = "org.seleniumhq.selenium" % "selenium-java" % seleniumVersion % "test"
val seleniumFirefox = "org.seleniumhq.selenium" % "selenium-firefox-driver" % seleniumVersion % "test"
val seleniumStack = Seq(seleniumJava, seleniumFirefox)

val akkaHttpCore         = "com.typesafe.akka"                  %% "akka-http-core"                      % akkaVersion
val akkaHttpExperimental = "com.typesafe.akka"                  %% "akka-http-experimental"              % akkaVersion
val akkaHttpTestkit      = "com.typesafe.akka"                  %% "akka-http-testkit"                   % akkaVersion % "test"
val akkaHttpSession      = "com.softwaremill.akka-http-session" %% "core"                                % "0.2.5"
val akkaStream           = "com.typesafe.akka"                  %% "akka-stream"                         % akkaVersion
val akkaPersistence      = "com.typesafe.akka"                  %% "akka-persistence"                    % akkaVersion
val akkaPersistenceQuery = "com.typesafe.akka"                  %% "akka-persistence-query-experimental" % akkaVersion
val akkaCluster          = "com.typesafe.akka"                  %% "akka-cluster"                        % akkaVersion
val akkaClusterMetrics   = "com.typesafe.akka"                  %% "akka-cluster-metrics"                % akkaVersion
val akkaClusterTools     = "com.typesafe.akka"                  %% "akka-cluster-tools"                  % akkaVersion
val akkaDistributedData  = "com.typesafe.akka"                  %% "akka-distributed-data-experimental"  % akkaVersion

val scorexCore           = "org.consensusresearch"              %% "scrypto"                             % "1.1.0"
val scorexStack = Seq(scorexCore)

val akkaStack = Seq(akkaHttpCore, akkaHttpExperimental, akkaHttpTestkit, akkaHttpSession, akkaDistributedData,
  akkaStream, akkaPersistence, akkaPersistenceQuery, akkaCluster, akkaClusterMetrics, akkaClusterTools)

val commonDependencies = unitTestingStack ++ loggingStack

lazy val commonSettings = SbtScalariform.scalariformSettings ++ Seq(
  scalariformPreferences := scalariformPreferences.value
    .setPreference(DoubleIndentClassDeclaration, true)
    .setPreference(PreserveSpaceBeforeArguments, true)
    .setPreference(CompactControlReadability, true)
    .setPreference(SpacesAroundMultiImports, false),
  organization := "com.softwaremill",
  version := "0.0.1-SNAPSHOT",
  scalaVersion := "2.11.8",
  scalacOptions ++= Seq("-unchecked", "-deprecation"),
  libraryDependencies ++= commonDependencies
)

def haltOnCmdResultError(result: Int) {
  if (result != 0) {
    throw new Exception("Build failed.")
  }
}

val updateNpm = baseDirectory map { bd =>
  println("Updating NPM dependencies")
  haltOnCmdResultError(Process("npm install", bd / ".." / "ui") !)
}

def npmTask(taskName: String) = (baseDirectory, streams) map { (bd, s) =>
  val localNpmCommand = "npm " + taskName
  def buildWebpack() = {
    Process(localNpmCommand, bd / ".." / "ui").!
  }
  println("Building with Webpack : " + taskName)
  haltOnCmdResultError(buildWebpack())
} dependsOn updateNpm


lazy val rootProject = (project in file("."))
  .settings(commonSettings: _*)
  .settings(
    name := "avalanchain",
    herokuFatJar in Compile := Some((assemblyOutputPath in backend in assembly).value),
    deployHeroku in Compile <<= (deployHeroku in Compile) dependsOn (assembly in backend)
  )
  .aggregate(backend, ui)

lazy val backend: Project = (project in file("backend"))
  .enablePlugins(BuildInfoPlugin)
  .settings(commonSettings)
  .settings(Revolver.settings)
  .settings(
    libraryDependencies ++= slickStack ++ akkaStack ++ scorexStack ++ circe ++ Seq(javaxMailSun, typesafeConfig)
      ++ Seq(pickling, sprayJson, kantan, yahoo),
    buildInfoPackage := "com.avalanchain.web.version",
    buildInfoObject := "BuildInfo",
    buildInfoKeys := Seq[BuildInfoKey](
      BuildInfoKey.action("buildDate")(new SimpleDateFormat("yyyy-MM-dd HH:mm").format(new Date())),
      // if the build is done outside of a git repository, we still want it to succeed
      BuildInfoKey.action("buildSha")(Try(Process("git rev-parse HEAD").!!.stripLineEnd).getOrElse("?"))),
    compile in Compile := {
      val compilationResult = (compile in Compile).value
      IO.touch(target.value / "compilationFinished")

      compilationResult
    },
    mainClass in Compile := Some("com.avalanchain.web.Main"),
    // We need to include the whole webapp, hence replacing the resource directory
    unmanagedResourceDirectories in Compile := {
      (unmanagedResourceDirectories in Compile).value ++ List(baseDirectory.value.getParentFile / ui.base.getName / "dist")
    },
    assemblyJarName in assembly := "avalanchain.jar",
    assembly <<= assembly dependsOn npmTask("run build")
  )

lazy val ui = (project in file("ui"))
  .settings(commonSettings: _*)
  .settings(test in Test <<= (test in Test) dependsOn npmTask("run test"))

lazy val uiTests = (project in file("ui-tests"))
  .settings(commonSettings: _*)
  .settings(
    parallelExecution := false,
    libraryDependencies ++= seleniumStack,
    test in Test <<= (test in Test) dependsOn npmTask("run build")
  ) dependsOn backend

RenameProject.settings
