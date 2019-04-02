﻿'    Copyright (C) 2018-2019 Robbie Ward
' 
'    This file is a part of Winapp2ool
' 
'    Winapp2ool is free software: you can redistribute it and/or modify
'    it under the terms of the GNU General Public License as published by
'    the Free Software Foundation, either version 3 of the License, or
'    (at your option) any later version.
'
'    Winap2ool is distributed in the hope that it will be useful,
'    but WITHOUT ANY WARRANTY; without even the implied warranty of
'    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
'    GNU General Public License for more details.
'
'    You should have received a copy of the GNU General Public License
'    along with Winapp2ool.  If not, see <http://www.gnu.org/licenses/>.
Option Strict On
Imports System.IO
Imports System.Text.RegularExpressions

''' <summary>
''' A program whose purpose is to observe, report, and attempt to repair errors in winapp2.ini
''' </summary>
Public Module WinappDebug
    ' File handlers
    Private winapp2File As iniFile = New iniFile(Environment.CurrentDirectory, "winapp2.ini")
    Private outputFile As iniFile = New iniFile(Environment.CurrentDirectory, "winapp2.ini", "winapp2-debugged.ini")
    ' Menu settings
    Private settingsChanged As Boolean = False
    Private scanSettingsChanged As Boolean = False
    ' Module parameters
    Private correctFormatting As Boolean = False
    Dim allEntryNames As New List(Of String)
    Dim numErrs As Integer = 0
    Dim correctSomeFormatting As Boolean = False
    Dim saveChangesMade As Boolean = False
    ' Lint Rules
    Dim lintCasing As New lintRule(True, True, "Casing", "improper CamelCasing", "fixing improper CamelCasing")
    Dim lintAlpha As New lintRule(True, True, "Alphabetization", "improper alphabetization", "fixing improper alphabetization")
    Dim lintWrongNums As New lintRule(True, True, "Improper Numbering", "improper key numbering", "fixing improper key numbering")
    Dim lintParams As New lintRule(True, True, "Parameters", "improper parameterization on FileKeys and ExcludeKeys", "fixing improper parameterization on FileKeys And ExcludeKeys")
    Dim lintFlags As New lintRule(True, True, "Flags", "improper RECURSE and REMOVESELF formatting", "fixing improper RECURSE and REMOVESELF formatting")
    Dim lintSlashes As New lintRule(True, True, "Slashes", "improper use of slashes (\)", "fixing improper use of slashes (\)")
    Dim lintDefaults As New lintRule(True, True, "Defaults", "Default=True or missing Default key", "enforcing Default=False")
    Dim lintDupes As New lintRule(True, True, "Duplicates", "duplicate key values", "removing keys with duplicated values")
    Dim lintExtraNums As New lintRule(True, True, "Uneeded Numbering", "use of numbers where there should not be", "removing numbers used where they shouldn't be")
    Dim lintMulti As New lintRule(True, True, "Multiples", "multiples of key types that should only occur once in an entry", "removing unneeded multiples of key types that should occur only once")
    Dim lintInvalid As New lintRule(True, True, "Invalid Values", "invalid key values", "fixing cer,tain types of invalid key values")
    Dim lintSyntax As New lintRule(True, True, "Syntax Errors", "some entries whose configuration will not run in CCleaner", "attempting to fix certain types of syntax errors")
    Dim lintOpti As New lintRule(False, False, "Optimizations", "situations where keys can be merged (experimental)", "automatic merging of keys (experimental)")
    Private currentLintRules As lintRule() = initLintRules()
    Private _saveFile As Boolean
    ' Regex 
    ReadOnly longReg As New Regex("HKEY_(C(URRENT_(USER$|CONFIG$)|LASSES_ROOT$)|LOCAL_MACHINE$|USERS$)")
    ReadOnly shortReg As New Regex("HK(C(C$|R$|U$)|LM$|U$)")
    ReadOnly secRefNums As New Regex("30(2([0-9])|3(0|1))")
    ReadOnly driveLtrs As New Regex("[a-zA-z]:")
    ReadOnly envVarRegex As New Regex("%[A-Za-z0-9]*%")


    ''' <summary>
    ''' The winapp2.ini file that will be linted
    ''' </summary>
    ''' <returns></returns>
    Public Property winappDebugFile1 As iniFile
        Get
            Return winapp2File
        End Get
        Set(value As iniFile)
            winapp2File = value
        End Set
    End Property

    ''' <summary>
    ''' Holds the save path for the linted file (overwrites the input file by default)
    ''' </summary>
    ''' <returns></returns>
    Public Property winappDebugFile3 As iniFile
        Get
            Return outputFile
        End Get
        Set(value As iniFile)
            outputFile = value
        End Set
    End Property

    Public Property saveChanges As Boolean
        Get
            Return _saveFile
        End Get
        Set
            _saveFile = Value
        End Set
    End Property

    ''' <summary>
    ''' Indicates whether or not winappdebug should attempt to repair errors it finds, disabled by default
    ''' </summary>
    ''' <returns></returns>
    Public Property CorrectFormatting1 As Boolean
        Get
            Return correctFormatting
        End Get
        Set(value As Boolean)
            correctFormatting = value
        End Set
    End Property

    ''' <summary>
    ''' The number of errors found by the linter
    ''' </summary>
    ''' <returns></returns>
    Public Property errorsFound As Integer
        Get
            Return numErrs
        End Get
        Set(value As Integer)
            numErrs = value
        End Set
    End Property

    ''' <summary>
    ''' The current rules for scans and repairs 
    ''' </summary>
    ''' <returns></returns>
    Public Property Rules1 As lintRule()
        Get
            Return currentLintRules
        End Get
        Set
            currentLintRules = Value
        End Set
    End Property

    ''' <summary>
    ''' Indicates whether some but not all repairs will run
    ''' </summary>
    ''' <returns></returns>
    Public Property CorrectSomeFormatting1 As Boolean
        Get
            Return correctSomeFormatting
        End Get
        Set(value As Boolean)
            correctSomeFormatting = value
        End Set
    End Property

    ''' <summary>
    ''' Resets the lint rules to their default states
    ''' </summary>
    Private Sub resetLintRules()
        For Each rule In Rules1
            rule.resetParams()
        Next
    End Sub

    ''' <summary>
    ''' Instantiates the default state of the lint rules
    ''' </summary>
    Private Function initLintRules() As lintRule()
        Return {lintCasing, lintAlpha, lintWrongNums, lintParams, lintFlags, lintSlashes,
            lintDefaults, lintDupes, lintExtraNums, lintMulti, lintInvalid, lintSyntax, lintOpti}
    End Function

    ''' <summary>
    ''' Resets the individual scan settings to their defaults
    ''' </summary>
    Private Sub resetScanSettings()
        resetLintRules()
        scanSettingsChanged = False
        CorrectSomeFormatting1 = False
    End Sub

    ''' <summary>
    ''' Handles the commandline args for WinappDebug
    ''' </summary>
    ''' WinappDebug specific command line args
    ''' -c          : enable autocorrect
    Public Sub handleCmdLine()
        initDefaultSettings()
        invertSettingAndRemoveArg(saveChanges, "-c")
        getFileAndDirParams(winappDebugFile1, New iniFile, winappDebugFile3)
        If Not cmdargs.Contains("UNIT_TESTING_HALT") Then initDebug()
    End Sub

    ''' <summary>
    ''' Restore the default state of the module's parameters
    ''' </summary>
    Private Sub initDefaultSettings()
        resetLintRules()
        winappDebugFile1.resetParams()
        winappDebugFile3.resetParams()
        settingsChanged = False
        CorrectFormatting1 = False
        saveChanges = False
        resetScanSettings()
    End Sub

    ''' <summary>
    ''' Prints the menu for individual scans and their repairs to the user
    ''' </summary>
    Private Sub printScansMenu()
        Console.WindowHeight = 46
        printMenuTop({"Enable or disable specific scans or repairs"})
        print(0, "Scan Options", leadingBlank:=True, trailingBlank:=True)
        For Each rule In currentLintRules
            print(5, rule.LintName1, rule.ScanText1, enStrCond:=rule.ShouldScan)
        Next
        print(0, "Repair Options", leadingBlank:=True, trailingBlank:=True)
        For i = 0 To currentLintRules.Count - 2
            Dim rule = currentLintRules(i)
            print(5, rule.LintName1, rule.RepairText1, enStrCond:=rule.ShouldRepair)
        Next
        ' Special case for the last repair option (closemenu flag)
        Dim lastRule = currentLintRules.Last
        print(5, lastRule.LintName1, lastRule.RepairText1, closeMenu:=Not scanSettingsChanged, enStrCond:=lastRule.ShouldRepair)
        print(2, "Scan And Repair", cond:=scanSettingsChanged, closeMenu:=True)
    End Sub

    ''' <summary>
    ''' Determines which if any lint rules have been modified and whether or not only some repairs are scheduled to run
    ''' </summary>
    Private Sub determineScanSettings()
        Dim repairAll = True
        Dim repairAny = False
        For Each rule In Rules1
            If rule.hasBeenChanged Then
                scanSettingsChanged = True
                If Not rule.ShouldRepair Then repairAll = False
            End If
            If rule.ShouldRepair Then repairAny = True
        Next
        If Not repairAll And repairAny Then
            CorrectFormatting1 = False
            CorrectSomeFormatting1 = True
        End If
    End Sub

    ''' <summary>
    ''' Handles the user input for the scan settings menu
    ''' </summary>
    ''' <param name="input">The String containing the user's input</param>
    Private Sub handleScanInput(input As String)
        ' Query the current lint settings states 
        determineScanSettings()
        Dim scanNums = {"1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11", "12", "13"}
        Dim repNums = {"14", "15", "16", "17", "18", "19", "20", "21", "22", "23", "24", "25", "26"}
        Select Case True
            Case input = "0"
                If scanSettingsChanged Then settingsChanged = True
                exitCode = True
            ' Enable/Disable individual scans
            Case scanNums.Contains(input)
                Dim ind = scanNums.ToList.IndexOf(input)
                toggleSettingParam(Rules1(ind).ShouldScan, "Scan", scanSettingsChanged)
                ' Force repair off if the scan is off
                If Not Rules1(ind).ShouldScan Then Rules1(ind).turnOff()
            ' Enable/Disable individual repairs
            Case repNums.Contains(input)
                Dim ind = repNums.ToList.IndexOf(input)
                toggleSettingParam(Rules1(ind).ShouldRepair, "Repair", scanSettingsChanged)
                ' Force scan on if the repair is on
                If Rules1(ind).ShouldRepair Then Rules1(ind).turnOn()
            Case input = "27" And scanSettingsChanged
                resetScanSettings()
                setHeaderText("Settings Reset")
            ' This isn't documented anywhere and is mostly intended as a debugging shortcut
            Case input = "alloff"
                For Each rule In currentLintRules
                    rule.turnOff()
                Next
            Case Else
                setHeaderText(invInpStr, True)
        End Select
    End Sub

    ''' <summary>
    ''' Prints the main menu to the user
    ''' </summary>
    Public Sub printMenu()
        printMenuTop({"Scan winapp2.ini For syntax and style errors, And attempt to repair them."})
        print(1, "Run (Default)", "Run the debugger")
        print(1, "File Chooser (winapp2.ini)", "Choose a new winapp2.ini name or path", leadingBlank:=True, trailingBlank:=True)
        print(5, "Toggle Saving", "saving the file after correcting errors", enStrCond:=saveChanges)
        print(1, "File Chooser (save)", "Save a copy of changes made instead of overwriting winapp2.ini", saveChanges, trailingBlank:=True)
        print(1, "Toggle Scan Settings", "Enable or disable individual scan and correction routines", leadingBlank:=Not saveChanges, trailingBlank:=True)
        print(0, $"Current winapp2.ini:  {replDir(winappDebugFile1.path)}", closeMenu:=Not saveChanges And Not settingsChanged)
        print(0, $"Current save target:  {replDir(winappDebugFile3.path)}", cond:=saveChanges, closeMenu:=Not settingsChanged)
        print(2, "WinappDebug", cond:=settingsChanged, closeMenu:=True)
    End Sub

    ''' <summary>
    ''' Handles the user's input from the menu
    ''' </summary>
    ''' <param name="input">The String containing the user's input</param>
    Public Sub handleUserInput(input As String)
        Select Case True
            Case input = "0"
                exitModule()
            Case input = "1" Or input = ""
                initDebug()
            Case input = "2"
                changeFileParams(winappDebugFile1, settingsChanged)
            Case input = "3"
                toggleSettingParam(saveChanges, "Saving", settingsChanged)
            Case input = "4" And saveChanges
                changeFileParams(winappDebugFile3, settingsChanged)
            Case (input = "4" And Not saveChanges) Or (input = "5" And saveChanges)
                initModule("Scan Settings", AddressOf printScansMenu, AddressOf handleScanInput)
                Console.WindowHeight = 30
            Case settingsChanged And ((input = "5" And Not saveChanges) Or (input = "6" And saveChanges))
                resetModuleSettings("WinappDebug", AddressOf initDefaultSettings)
            Case Else
                setHeaderText(invInpStr, True)
        End Select
    End Sub

    ''' <summary>
    ''' Validates and debugs the ini file, informs the user upon completion
    ''' </summary>
    Private Sub initDebug()
        winappDebugFile1.validate()
        If pendingExit() Then Exit Sub
        Dim wa2 As New winapp2file(winappDebugFile1)
        Console.Clear()
        print(0, tmenu("Beginning analysis of winapp2.ini"), closeMenu:=True)
        cwl()
        debug(wa2)
        setHeaderText("Debug Complete")
        print(0, tmenu("Completed analysis of winapp2.ini"))
        print(0, menuStr03)
        print(0, $"{errorsFound} possible errors were detected.")
        print(0, $"Number of entries: {winappDebugFile1.sections.Count}", trailingBlank:=True)
        rewriteChanges(wa2)
        print(0, anyKeyStr, closeMenu:=True)
        If Not SuppressOutput Then Console.ReadKey()
    End Sub

    ''' <summary>
    ''' Performs syntax and format checking on a winapp2.ini format ini file
    ''' </summary>
    ''' <param name="fileToBeDebugged">A winapp2file object to be processed</param>
    Public Sub debug(ByRef fileToBeDebugged As winapp2file)
        errorsFound = 0
        allEntryNames = New List(Of String)
        processEntries(fileToBeDebugged)
        fileToBeDebugged.rebuildToIniFiles()
        alphabetizeEntries(fileToBeDebugged)
    End Sub

    ''' <summary>
    ''' Initiates the debugger on each entry in a given winapp2file
    ''' </summary>
    ''' <param name="winapp">The winapp2file to be debugged</param>
    Private Sub processEntries(ByRef winapp As winapp2file)
        For Each entryList In winapp.winapp2entries
            If entryList.Count = 0 Then Continue For
            entryList.ForEach(Sub(entry) processEntry(entry))
        Next
    End Sub

    ''' <summary>
    ''' Alphabetizes all the entries in a winapp2.ini file and observes any that were out of place
    ''' </summary>
    ''' <param name="winapp">The object containing the winapp2.ini being operated on</param>
    Private Sub alphabetizeEntries(ByRef winapp As winapp2file)
        For Each innerFile In winapp.entrySections
            Dim unsortedEntryList As List(Of String) = innerFile.namesToListOfStr
            Dim sortedEntryList As List(Of String) = sortEntryNames(innerFile)
            findOutOfPlace(unsortedEntryList, sortedEntryList, "Entry", innerFile.getLineNumsFromSections)
            If lintAlpha.fixFormat Then innerFile.sortSections(sortedEntryList)
        Next
    End Sub

    ''' <summary>
    ''' Overwrites the file on disk with any changes we've made if we are saving them
    ''' </summary>
    ''' <param name="winapp2file">The object representing winapp2.ini</param>
    Private Sub rewriteChanges(ByRef winapp2file As winapp2file)
        If saveChanges Then
            print(0, "Saving changes, do not close winapp2ool or data loss may occur...", leadingBlank:=True)
            winappDebugFile3.overwriteToFile(winapp2file.winapp2string)
            print(0, "Finished saving changes.", trailingBlank:=True)
        End If
    End Sub

    ''' <summary>
    ''' Construct a list of neighbors for strings in a list
    ''' </summary>
    ''' <param name="someList">A list of strings</param>
    ''' <param name="neighborList">The paired values of neighbors in the list of strings</param>
    Private Sub buildNeighborList(someList As List(Of String), neighborList As List(Of KeyValuePair(Of String, String)))
        neighborList.Add(New KeyValuePair(Of String, String)("first", someList(1)))
        For i = 1 To someList.Count - 2
            neighborList.Add(New KeyValuePair(Of String, String)(someList(i - 1), someList(i + 1)))
        Next
        neighborList.Add(New KeyValuePair(Of String, String)(someList(someList.Count - 2), "last"))
    End Sub

    ''' <summary>
    ''' Assess a list and its sorted state to observe changes in neighboring strings
    ''' </summary>
    ''' eg. changes made to string ordering through sorting
    ''' <param name="someList">A list of strings</param>
    ''' <param name="sortedList">The sorted state of someList</param>
    ''' <param name="findType">The type of neighbor checking (keyType for iniKey values)</param>
    ''' <param name="LineCountList">A list containing the line counts of the Strings in someList</param>
    ''' <param name="oopBool">Optional Boolean that reports whether or not there are any out of place entries in the list</param>
    Private Sub findOutOfPlace(ByRef someList As List(Of String), ByRef sortedList As List(Of String), ByVal findType As String, ByRef LineCountList As List(Of Integer), Optional ByRef oopBool As Boolean = False)
        'Only try to find out of place keys when there's more than one
        If someList.Count > 1 Then
            Dim misplacedEntries As New List(Of String)
            ' Learn the neighbors of each string in each respective list
            Dim initalNeighbors As New List(Of KeyValuePair(Of String, String))
            Dim sortedNeigbors As New List(Of KeyValuePair(Of String, String))
            buildNeighborList(someList, initalNeighbors)
            buildNeighborList(sortedList, sortedNeigbors)
            ' Make sure at least one of the neighbors of each string are the same in both the sorted and unsorted state, otherwise the string has moved 
            For i = 0 To someList.Count - 1
                Dim sind As Integer = sortedList.IndexOf(someList(i))
                If Not (initalNeighbors(i).Key = sortedNeigbors(sind).Key And initalNeighbors(i).Value = sortedNeigbors(sind).Value) Then misplacedEntries.Add(someList(i))
            Next
            ' Report any misplaced entries back to the user
            For Each entry In misplacedEntries
                Dim recInd = someList.IndexOf(entry)
                Dim sortInd = sortedList.IndexOf(entry)
                Dim curLine = LineCountList(recInd)
                Dim sortLine = LineCountList(sortInd)
                If (recInd = sortInd Or curLine = sortLine) Then Continue For
                entry = If(findType = "Entry", entry, $"{findType & recInd + 1}={entry}")
                If Not oopBool Then oopBool = True
                customErr(LineCountList(recInd), $"{findType} alphabetization", {$"{entry} appears to be out of place", $"Current line: {curLine}", $"Expected line: {sortLine}"})
            Next
        End If
    End Sub

    ''' <summary>
    ''' Audits a keyList of winapp2.ini format iniKeys for errors, alerting the user and correcting where possible.
    ''' </summary>
    ''' <param name="kl">A keylist to audit</param>
    ''' <param name="processKey">The function that audits the keys of the keyType provided in the keyList</param>
    ''' <param name="hasF">Optional boolean for the ExcludeKey case</param>
    ''' <param name="hasR">Optional boolean for the ExcludeKey case</param>
    Private Sub processKeyList(ByRef kl As keyList, processKey As Func(Of iniKey, iniKey), Optional ByRef hasF As Boolean = False, Optional ByRef hasR As Boolean = False)
        If kl.keyCount = 0 Then Exit Sub
        Dim curNum As Integer = 1
        Dim curStrings As New List(Of String)
        Dim dupes As New keyList
        For Each key In kl.keys
            ' Preprocess key formats before doing key specific audits
            Select Case key.keyType
                Case "ExcludeKey"
                    cFormat(key, curNum, curStrings, dupes)
                    pExcludeKey(key, hasF, hasR)
                Case "Detect", "DetectFile"
                    If key.typeIs("Detect") Then chkPathFormatValidity(key, True)
                    cFormat(key, curNum, curStrings, dupes, kl.keyCount = 1)
                Case "RegKey"
                    chkPathFormatValidity(key, True)
                    cFormat(key, curNum, curStrings, dupes)
                Case "Warning", "DetectOS", "SpecialDetect", "LangSecRef", "Section", "Default"
                    ' No keys of these types should occur more than once per entry
                    If curNum > 1 And lintMulti.ShouldScan Then
                        fullKeyErr(key, $"Multiple {key.keyType} detected.")
                        If lintMulti.fixFormat Then dupes.add(key)
                    End If
                    cFormat(key, curNum, curStrings, dupes, True)
                    ' Scan for invalid values in LangSecRef and SpecialDetect
                    If key.typeIs("SpecialDetect") Then chkCasing(key, {"DET_CHROME", "DET_MOZILLA", "DET_THUNDERBIRD", "DET_OPERA"}, key.value, 1)
                    fullKeyErr(key, "LangSecRef holds an invalid value.", lintInvalid.ShouldScan And key.typeIs("LangSecRef") And Not secRefNums.IsMatch(key.value))
                    ' Enforce that Default=False
                    fullKeyErr(key, "All entries should be disabled by default (Default=False).", lintDefaults.ShouldScan And Not key.vIs("False") And key.typeIs("Default"), lintDefaults.fixFormat, key.value, "False")
                Case Else
                    cFormat(key, curNum, curStrings, dupes)
            End Select
            key = processKey(key)
        Next
        kl.remove(dupes.keys)
        sortKeys(kl, dupes.keyCount > 0)
        If kl.typeIs("FileKey") Then cOptimization(kl)
    End Sub

    ''' <summary>
    ''' Does some basic formatting checks that apply to most winapp2.ini format iniKey objects
    ''' </summary>
    ''' <param name="key">The current iniKey object being processed</param>
    ''' <param name="keyNumber">The current expected key number</param>
    ''' <param name="keyValues">The current list of observed inikey values</param>
    ''' <param name="dupeList">A tracking list of detected duplicate iniKeys</param>
    Private Sub cFormat(ByVal key As iniKey, ByRef keyNumber As Integer, ByRef keyValues As List(Of String), ByRef dupeList As keyList, Optional noNumbers As Boolean = False)
        ' Check for duplicates
        Dim lowerCaseKeyValue = key.value.ToLower
        If chkDupes(keyValues, lowerCaseKeyValue) And lintDupes.ShouldScan Then
            Dim duplicateKeyStr As String = $"{key.keyType}{If(Not noNumbers, (keyValues.IndexOf(lowerCaseKeyValue) + 1).ToString, "")}={key.value}"
            customErr(key.lineNumber, "Duplicate key value found", {$"Key:            {key.toString}", $"Duplicates:     {duplicateKeyStr}"})
            If lintDupes.fixFormat Then dupeList.add(key)
        End If
        ' Check for both types of numbering errors (incorrect and unneeded) 
        Dim hasNumberingError As Boolean = If(noNumbers, Not key.nameIs(key.keyType), Not key.nameIs(key.keyType & keyNumber))
        Dim numberingErrStr As String = If(noNumbers, "Detected unnecessary numbering.", $"{key.keyType} entry is incorrectly numbered.")
        Dim fixedStr As String = If(noNumbers, key.keyType, key.keyType & keyNumber)
        inputMismatchErr(key.lineNumber, numberingErrStr, key.name, fixedStr, If(noNumbers, lintExtraNums.ShouldScan, lintWrongNums.ShouldScan) And hasNumberingError)
        fixStr(If(noNumbers, lintExtraNums.fixFormat, lintWrongNums.fixFormat) And hasNumberingError, key.name, fixedStr)
        ' Scan for and fix any use of incorrect slashes (except in Warning keys) or trailing semicolons
        fullKeyErr(key, "Forward slash (/) detected in lieu of backslash (\).", Not key.typeIs("Warning") And lintSlashes.ShouldScan And key.vHas(CChar("/")), lintSlashes.fixFormat, key.value, key.value.Replace(CChar("/"), CChar("\")))
        fullKeyErr(key, "Trailing semicolon (;).", key.toString.Last = CChar(";") And lintParams.ShouldScan, lintParams.fixFormat, key.value, key.value.TrimEnd(CChar(";")))
        ' Do some formatting checks for environment variables if needed
        If {"FileKey", "ExcludeKey", "DetectFile"}.Contains(key.keyType) Then cEnVar(key)
        keyNumber += 1
    End Sub

    ''' <summary>
    ''' Checks a String for casing errors against a provided array of cased strings, returns the input string if no error is detected
    ''' </summary>
    ''' <param name="caseArray">The parent array of cased Strings</param>
    ''' <param name="inputText">The String to be checked for casing errors</param>
    Private Function getCasedString(caseArray As String(), inputText As String) As String
        For Each casedText In caseArray
            If inputText.Equals(casedText, StringComparison.InvariantCultureIgnoreCase) Then Return casedText
        Next
        Return inputText
    End Function

    ''' <summary>
    ''' Assess the formatting of any %EnvironmentVariables% in a given iniKey
    ''' </summary>
    ''' <param name="key">An iniKey object to be processed</param>
    Private Sub cEnVar(key As iniKey)
        ' Valid Environmental Variables for winapp2.ini
        Dim enVars As String() = {"AllUsersProfile", "AppData", "CommonAppData", "CommonProgramFiles",
            "Documents", "HomeDrive", "LocalAppData", "LocalLowAppData", "Music", "Pictures", "ProgramData", "ProgramFiles", "Public",
            "RootDir", "SystemDrive", "SystemRoot", "Temp", "Tmp", "UserName", "UserProfile", "Video", "WinDir"}
        fullKeyErr(key, "%EnvironmentVariables% must be surrounded on both sides by a single '%' character.", key.vHas("%") And envVarRegex.Matches(key.value).Count = 0)
        For Each m As Match In envVarRegex.Matches(key.value)
            Dim strippedText As String = m.ToString.Trim(CChar("%"))
            chkCasing(key, enVars, strippedText, 1)
        Next
    End Sub

    ''' <summary>
    ''' Does basic syntax and formatting audits that apply across all keys, returns false iff a key is malformed
    ''' </summary>
    ''' <param name="key">an iniKey object to be checked for errors</param>
    ''' <returns></returns>
    Private Function cValidity(key As iniKey) As Boolean
        ' Valid winapp2.ini keyTypes
        Dim validCmds As String() = {"Default", "Detect", "DetectFile", "DetectOS", "ExcludeKey",
                           "FileKey", "LangSecRef", "RegKey", "Section", "SpecialDetect", "Warning"}
        If key.typeIs("DeleteMe") Then Return False
        ' Check for leading or trailing whitespace, do this always as spaces in the name interfere with proper keyType identification
        If key.name.StartsWith(" ") Or key.name.EndsWith(" ") Or key.value.StartsWith(" ") Or key.value.EndsWith(" ") Then
            fullKeyErr(key, "Detected unwanted whitespace in iniKey", True)
            fixStr(True, key.value, key.value.Trim(CChar(" ")))
            fixStr(True, key.name, key.name.Trim(CChar(" ")))
            fixStr(True, key.keyType, key.keyType.Trim(CChar(" ")))
        End If
        ' Make sure the keyType is valid
        chkCasing(key, validCmds, key.keyType, 0)
        Return True
    End Function

    Private Sub chkCasing(ByRef key As iniKey, casedArray As String(), ByRef strToChk As String, chkType As Integer)
        ' Get the properly cased string
        Dim casedString = getCasedString(casedArray, strToChk)
        ' Determine if there's a casing error
        Dim hasCasingErr = Not casedString.Equals(strToChk) And casedArray.Contains(casedString)
        Dim replacementText = ""
        Select Case chkType
            ' keyType casing check
            Case 0
                replacementText = key.name.Replace(key.keyType, casedString)
            ' Value casing check
            Case 1
                replacementText = key.value.Replace(key.value, casedString)
        End Select
        fullKeyErr(key, $"{casedString} has a casing error.", hasCasingErr And lintCasing.ShouldScan, lintCasing.fixFormat, strToChk, replacementText)
        fullKeyErr(key, $"Invalid data provided: {strToChk}{Environment.NewLine}Valid data: {arrayToStr(casedArray)}", Not casedArray.Contains(casedString) And lintInvalid.ShouldScan)
    End Sub

    ''' <summary>
    ''' Returns the contents of the array as a single comma delimited String
    ''' </summary>
    ''' <param name="given">A given array of Strings</param>
    ''' <returns></returns>
    Private Function arrayToStr(given As String()) As String
        Dim out = ""
        For i = 0 To given.Count - 2
            out += given(i) & ", "
        Next
        out += given.Last
        Return out
    End Function

    ''' <summary>
    ''' Processes a FileKey format winapp2.ini iniKey object and checks it for errors, correcting where possible
    ''' </summary>
    ''' <param name="key">A winap2.ini FileKey format iniKey object</param>
    Private Function pFileKey(key As iniKey) As iniKey
        ' Pipe symbol checks
        Dim iteratorCheckerList() As String = Split(key.value, "|")
        fullKeyErr(key, "Missing pipe (|) in FileKey.", Not key.vHas("|"))
        ' Captures any incident of semi colons coming before the first pipe symbol
        fullKeyErr(key, "Semicolon (;) found before pipe (|).", key.vHas(";") And key.value.IndexOf(";") < key.value.IndexOf("|"))
        ' Check for incorrect spellings of RECURSE or REMOVESELF
        If iteratorCheckerList.Length > 2 Then fullKeyErr(key, "RECURSE or REMOVESELF is incorrectly spelled, or there are too many pipe (|) symbols.", Not iteratorCheckerList(2).Contains("RECURSE") And Not iteratorCheckerList(2).Contains("REMOVESELF"))
        ' Check for missing pipe symbol on recurse and removeself, fix them if detected
        Dim flags As New List(Of String) From {"RECURSE", "REMOVESELF"}
        flags.ForEach(Sub(flagStr) fullKeyErr(key, $"Missing pipe (|) before {flagStr}.", lintFlags.ShouldScan And key.vHas(flagStr) And Not key.vHas($"|{flagStr}"), lintFlags.fixFormat, key.value, key.value.Replace(flagStr, $"|{flagStr}")))
        ' Make sure VirtualStore folders point to the correct place
        inputMismatchErr(key.lineNumber, "Incorrect VirtualStore location.", key.value, "%LocalAppData%\VirtualStore\Program Files*\", key.vHas("\virtualStore\p", True) And Not key.vHasAny({"programdata", "program files*", "program*"}, True))
        ' Backslash checks, fix if detected
        fullKeyErr(key, "Backslash (\) found before pipe (|).", lintSlashes.ShouldScan And key.vHas("%\|"), lintSlashes.fixFormat, key.value, key.value.Replace("%\|", "%|"))
        fullKeyErr(key, "Missing backslash (\) after %EnvironmentVariable%.", key.vHas("%") And Not key.vHasAny({"%|", "%\"}))
        ' Get the parameters given to the file key and sort them 
        Dim keyParams As New winapp2KeyParameters(key)
        Dim argsStrings As New List(Of String)
        Dim dupeArgs As New List(Of String)
        ' Check for duplicate args
        For Each arg In keyParams.argsList
            If chkDupes(argsStrings, arg) And lintParams.ShouldScan Then
                err(key.lineNumber, "Duplicate FileKey parameter found", arg)
                If lintParams.fixFormat Then dupeArgs.Add(arg)
            End If
        Next
        ' Remove any duplicate arguments from the key parameters and reconstruct keys we've modified above
        If lintParams.fixFormat Then
            dupeArgs.ForEach(Sub(arg) keyParams.argsList.Remove(arg))
            keyParams.reconstructKey(key)
        End If
        Return key
    End Function

    ''' <summary>
    ''' Checks the validity of the keys in an entry and removes any that are too problematic to continue with
    ''' </summary>
    ''' <param name="entry">A winapp2entry object to be audited</param>
    Private Sub validateKeys(ByRef entry As winapp2entry)
        For Each lst In entry.keyListList
            Dim brokenKeys As New keyList
            lst.keys.ForEach(Sub(key) brokenKeys.add(key, Not cValidity(key)))
            lst.remove(brokenKeys.keys)
            entry.errorKeys.remove(brokenKeys.keys)
        Next
        ' Attempt to assign keys that had errors to their intended lists
        For Each key In entry.errorKeys.keys
            For Each lst In entry.keyListList
                lst.add(key, key.typeIs(lst.keyType))
            Next
        Next
    End Sub

    ''' <summary>
    ''' Processes a winapp2entry object (generated from a winapp2.ini format iniKey object) for errors 
    ''' </summary>
    ''' <param name="entry">The winapp2entry object to be processed</param>
    Private Sub processEntry(ByRef entry As winapp2entry)
        Dim entryLinesList As New List(Of Integer)
        Dim hasFileExcludes As Boolean = False
        Dim hasRegExcludes As Boolean = False
        ' Check for duplicate names that are differently cased 
        fullNameErr(chkDupes(allEntryNames, entry.name), entry, "Duplicate entry name detected")
        ' Check that the entry is named properly 
        fullNameErr(Not entry.name.EndsWith(" *"), entry, "All entries must End In ' *'")
        ' Confirm the validity of keys and remove any broken ones before continuing
        validateKeys(entry)
        ' Process the entry's keylists in winapp2.ini order
        processKeyList(entry.detectOS, AddressOf voidDelegate)
        processKeyList(entry.sectionKey, AddressOf voidDelegate)
        processKeyList(entry.langSecRef, AddressOf voidDelegate)
        processKeyList(entry.specialDetect, AddressOf voidDelegate)
        processKeyList(entry.detects, AddressOf voidDelegate)
        processKeyList(entry.detectFiles, AddressOf pDetectFile)
        processKeyList(entry.defaultKey, AddressOf voidDelegate)
        processKeyList(entry.warningKey, AddressOf voidDelegate)
        processKeyList(entry.fileKeys, AddressOf pFileKey)
        processKeyList(entry.regKeys, AddressOf voidDelegate)
        processKeyList(entry.excludeKeys, AddressOf voidDelegate, hasFileExcludes, hasRegExcludes)
        ' Make sure we only have LangSecRef if we have LangSecRef at all
        fullNameErr(entry.langSecRef.keyCount <> 0 And entry.sectionKey.keyCount <> 0 And lintSyntax.ShouldScan, entry, "Section key found alongside LangSecRef key, only one is required.")
        ' Make sure we have at least 1 valid detect key 
        fullNameErr(entry.detectOS.keyCount + entry.detects.keyCount + entry.specialDetect.keyCount + entry.detectFiles.keyCount = 0, entry, "Entry has no valid detection keys (Detect, DetectFile, DetectOS, SpecialDetect)")
        ' Make sure we have at least 1 FileKey or RegKey
        fullNameErr(entry.fileKeys.keyCount + entry.regKeys.keyCount = 0, entry, "Entry has no valid FileKeys or RegKeys")
        ' If we don't have FileKeys or RegKeys, we shouldn't have ExcludeKeys.
        fullNameErr(entry.excludeKeys.keyCount > 0 And entry.fileKeys.keyCount + entry.regKeys.keyCount = 0, entry, "Entry has ExcludeKeys but no valid FileKeys or RegKeys")
        ' Make sure that if we have file Excludes, we have FileKeys
        fullNameErr(entry.fileKeys.keyCount = 0 And hasFileExcludes, entry, "ExcludeKeys targeting filesystem locations found without any corresponding FileKeys")
        ' Likewise for registry
        fullNameErr(entry.regKeys.keyCount = 0 And hasRegExcludes, entry, "ExcludeKeys targeting registry locations found without any corresponding RegKeys")
        ' Make sure we have a Default key.
        fullNameErr(entry.defaultKey.keyCount = 0 And lintDefaults.ShouldScan, entry, "Entry is missing a Default key")
        entry.defaultKey.add(New iniKey("Default=False"), lintDefaults.fixFormat And entry.defaultKey.keyCount = 0)
    End Sub

    ''' <summary>
    ''' This method does nothing by design
    ''' </summary>
    ''' <param name="key">An iniKey object to do nothing with</param>
    Private Function voidDelegate(key As iniKey) As iniKey
        Return key
    End Function

    ''' <summary>
    ''' Processes a DetectFile format winapp2.ini iniKey objects and checks it for errors, correcting where possible
    ''' </summary>
    ''' <param name="key">A winapp2.ini DetectFile format iniKey</param>
    ''' <returns></returns>
    Private Function pDetectFile(key As iniKey) As iniKey
        ' Trailing Backslashes
        fullKeyErr(key, "Trailing backslash (\) found in DetectFile", lintSlashes.ShouldScan _
            And key.value.Last = CChar("\"), lintSlashes.fixFormat, key.value, key.value.TrimEnd(CChar("\")))
        ' Nested wildcards
        If key.vHas("*") Then
            Dim splitDir As String() = key.value.Split(CChar("\"))
            For i = 0 To splitDir.Count - 1
                fullKeyErr(key, "Nested wildcard found in DetectFile", splitDir(i).Contains("*") And i <> splitDir.Count - 1)
            Next
        End If
        ' Make sure that DetectFile paths point to a filesystem location
        chkPathFormatValidity(key, False)
        Return key
    End Function

    ''' <summary>
    ''' Audits the syntax of file system and registry paths
    ''' </summary>
    ''' <param name="key">An iniKey to be audited</param>
    ''' <param name="isRegistry">Specifies whether the given key holds a registry path</param>
    Private Sub chkPathFormatValidity(key As iniKey, isRegistry As Boolean)
        ' Remove the flags from ExcludeKeys if we have them before getting the first directory portion
        Dim rootStr As String = If(key.keyType <> "ExcludeKey", getFirstDir(key.value), getFirstDir(pathFromExcludeKey(key)))
        ' Ensure that registry paths have a valid hive and file paths have either a variable or a drive letter
        fullKeyErr(key, "Invalid registry path detected.", isRegistry And Not longReg.IsMatch(rootStr) And Not shortReg.IsMatch(rootStr))
        fullKeyErr(key, "Invalid file system path detected.", Not isRegistry And Not driveLtrs.IsMatch(rootStr) And Not rootStr.StartsWith("%"))
    End Sub

    ''' <summary>
    ''' Returns the first portion of a registry or filepath parameterization
    ''' </summary>
    ''' <param name="val">The directory listing to be split</param>
    ''' <returns></returns>
    Public Function getFirstDir(val As String) As String
        Return val.Split(CChar("\"))(0)
    End Function

    ''' <summary>
    ''' Checks whether the current value appears in the given list of strings (case insensitive). Returns true if there is a duplicate,
    ''' otherwise, adds the current value to the list and returns false.
    ''' </summary>
    ''' <param name="valueStrings">A list of strings holding observed values</param>
    ''' <param name="currentValue">The current value to be audited</param>
    ''' <returns></returns>
    Private Function chkDupes(ByRef valueStrings As List(Of String), currentValue As String) As Boolean
        For Each value In valueStrings
            If currentValue.Equals(value, StringComparison.InvariantCultureIgnoreCase) Then Return True
        Next
        valueStrings.Add(currentValue)
        Return False
    End Function

    ''' <summary>
    ''' Sorts a keylist alphabetically with winapp2.ini precedence applied to the key values
    ''' </summary>
    ''' <param name="kl">The keylist to be sorted</param>
    ''' <param name="hadDuplicatesRemoved">The boolean indicating whether keys have been removed from this list</param>
    Private Sub sortKeys(ByRef kl As keyList, hadDuplicatesRemoved As Boolean)
        If Not lintAlpha.ShouldScan Or kl.keyCount <= 1 Then Exit Sub
        Dim keyValues = kl.toListOfStr(True)
        Dim sortedKeyValues = replaceAndSort(keyValues, "|", " \ \")
        ' Rewrite the alphabetized keys back into the keylist (also fixes numbering)
        Dim keysOutOfPlace = False
        findOutOfPlace(keyValues, sortedKeyValues, kl.keyType, kl.lineNums, keysOutOfPlace)
        If (keysOutOfPlace Or hadDuplicatesRemoved) And lintAlpha.fixFormat And (lintWrongNums.fixFormat Or lintExtraNums.fixFormat) Then
            kl.renumberKeys(sortedKeyValues)
        End If
    End Sub

    ''' <summary>
    ''' Processes a list of ExcludeKey format winapp2.ini iniKey objects and checks them for errors, correcting where possible
    ''' </summary>
    ''' <param name="key">A winapp2.ini ExcludeKey format iniKey object</param>
    ''' <param name="hasF">Indicates whether the given list excludes filesystem locations</param>
    ''' <param name="hasR">Indicates whether the given list excludes registry locations</param>
    Private Sub pExcludeKey(ByRef key As iniKey, ByRef hasF As Boolean, ByRef hasR As Boolean)
        Select Case True
            Case key.vHasAny({"FILE|", "PATH|"})
                hasF = True
                chkPathFormatValidity(key, False)
                fullKeyErr(key, "Missing backslash (\) before pipe (|) in ExcludeKey.", (key.vHas("|") And Not key.vHas("\|")))
            Case key.vHas("REG|")
                hasR = True
                chkPathFormatValidity(key, True)
            Case Else
                If key.value.StartsWith("FILE") Or key.value.StartsWith("PATH") Or key.value.StartsWith("REG") Then
                    fullKeyErr(key, "Missing pipe symbol after ExcludeKey flag)")
                    Exit Sub
                End If
                fullKeyErr(key, "No valid exclude flag (FILE, PATH, or REG) found in ExcludeKey.")
        End Select
    End Sub

    ''' <summary>
    ''' Returns the value from an ExcludeKey with the Flag parameter removed as a String
    ''' </summary>
    ''' <param name="key">An ExcludeKey iniKey</param>
    ''' <returns></returns>
    Private Function pathFromExcludeKey(key As iniKey) As String
        Dim pathFromKey As String = key.value.TrimStart(CType("FILE|", Char()))
        pathFromKey = pathFromKey.TrimStart(CType("PATH|", Char()))
        pathFromKey = pathFromKey.TrimStart(CType("REG|", Char()))
        Return pathFromKey
    End Function

    ''' <summary>
    ''' Attempts to merge FileKeys together if syntactically possible.
    ''' </summary>
    ''' <param name="kl">A list of winapp2.ini FileKey format iniFiles</param>
    Private Sub cOptimization(ByRef kl As keyList)
        If kl.keyCount < 2 Or Not lintOpti.ShouldScan Then Exit Sub
        Dim dupes As New keyList
        Dim newKeys As New keyList
        Dim flagList As New List(Of String)
        Dim paramList As New List(Of String)
        newKeys.add(kl.keys)
        For i = 0 To kl.keyCount - 1
            Dim tmpWa2 As New winapp2KeyParameters(kl.keys(i))
            ' If we have yet to record any params, record them and move on
            If paramList.Count = 0 Then trackParamAndFlags(paramList, flagList, tmpWa2) : Continue For
            ' This should handle the case where for a FileKey: 
            ' The folder provided has appeared in another key
            ' The flagstring (RECURSE, REMOVESELF, "") for both keys matches
            ' The first appearing key should have its parameters appended to and the second appearing key should be removed
            If paramList.Contains(tmpWa2.pathString) Then
                For j = 0 To paramList.Count - 1
                    If tmpWa2.pathString = paramList(j) And tmpWa2.flagString = flagList(j) Then
                        Dim keyToMergeInto As New winapp2KeyParameters(kl.keys(j))
                        Dim mergeKeyStr As String = ""
                        addArgs(mergeKeyStr, keyToMergeInto)
                        tmpWa2.argsList.ForEach(Sub(arg) mergeKeyStr += $";{arg}")
                        If tmpWa2.flagString <> "None" Then mergeKeyStr += $"|{tmpWa2.flagString}"
                        dupes.add(kl.keys(i))
                        newKeys.keys(j) = New iniKey(mergeKeyStr)
                        Exit For
                    End If
                Next
                trackParamAndFlags(paramList, flagList, tmpWa2)
            Else
                trackParamAndFlags(paramList, flagList, tmpWa2)
            End If
        Next
        If dupes.keyCount > 0 Then
            newKeys.remove(dupes.keys)
            For i = 0 To newKeys.keyCount - 1
                newKeys.keys(i).name = $"FileKey{i + 1}"
            Next
            printOptiSect("Optimization opportunity detected", kl)
            printOptiSect("The following keys can be merged into other keys:", dupes)
            printOptiSect("The resulting keyList will be reduced to: ", newKeys)
            If lintOpti.fixFormat Then kl = newKeys
        End If
    End Sub

    ''' <summary>
    ''' Prints output from the Optimization function
    ''' </summary>
    ''' <param name="boxStr">The text to go in the optimization section box</param>
    ''' <param name="kl">The list of keys to be printed beneath the box</param>
    Private Sub printOptiSect(boxStr As String, kl As keyList)
        cwl() : print(0, tmenu(boxStr), closeMenu:=True) : cwl()
        kl.keys.ForEach(Sub(key) cwl(key.toString))
        cwl()
    End Sub

    ''' <summary>
    ''' Tracks params and flags from a winapp2key
    ''' </summary>
    ''' <param name="paramList">The list of params observed</param>
    ''' <param name="flagList">The list of flags observed</param>
    ''' <param name="params">The current set of params and flags</param>
    Private Sub trackParamAndFlags(ByRef paramList As List(Of String), ByRef flagList As List(Of String), params As winapp2KeyParameters)
        paramList.Add(params.pathString)
        flagList.Add(params.flagString)
    End Sub

    ''' <summary>
    ''' Constructs a new iniKey in an attempt to merge keys together
    ''' </summary>
    ''' <param name="tmpKeyStr">The string to contain the new key text</param>
    ''' <param name="tmp2wa2">A set of parameters to append</param>
    Private Sub addArgs(ByRef tmpKeyStr As String, tmp2wa2 As winapp2KeyParameters)
        appendStrs({$"{tmp2wa2.keyType}{tmp2wa2.keyNum}=", $"{tmp2wa2.pathString}|", tmp2wa2.argsList(0)}, tmpKeyStr)
        If tmp2wa2.argsList.Count > 1 Then
            For i = 1 To tmp2wa2.argsList.Count - 1
                tmpKeyStr += $";{tmp2wa2.argsList(i)}"
            Next
        End If
    End Sub

    ''' <summary>
    ''' Prints an error when data is received that does not match an expected value.
    ''' </summary>
    ''' <param name="linecount">The line number of the error</param>
    ''' <param name="err">The string containing the output error text</param>
    ''' <param name="received">The (erroneous) input data</param>
    ''' <param name="expected">The expected data</param>
    ''' <param name="cond">Optional condition under which to display the error</param>
    Private Sub inputMismatchErr(linecount As Integer, err As String, received As String, expected As String, Optional cond As Boolean = True)
        If cond Then customErr(linecount, err, {$"Expected: {expected}", $"Found: {received}"})
    End Sub

    ''' <summary>
    ''' Prints an error when invalid data is received.
    ''' </summary>
    ''' <param name="linecount">The line number of the error</param>
    ''' <param name="errTxt">The string containing the output error text</param>
    ''' <param name="received">The (erroneous) input data</param>
    Private Sub err(linecount As Integer, errTxt As String, received As String)
        customErr(linecount, errTxt, {$"Command: {received}"})
    End Sub

    ''' <summary>
    ''' Prints an error followed by the [Full Name *] of the entry to which it belongs
    ''' </summary>
    ''' <param name="cond">The condition under which to print</param>
    ''' <param name="entry">The entry containing an error</param>
    ''' <param name="errTxt">The String containing the text to be printed to the user</param>
    Private Sub fullNameErr(cond As Boolean, entry As winapp2entry, errTxt As String)
        If cond Then customErr(entry.lineNum, errTxt, {$"Entry Name: {entry.fullName}"})
    End Sub

    ''' <summary>
    ''' Prints an error whose output text contains an iniKey string
    ''' </summary>
    ''' <param name="key">The inikey to be printed</param>
    ''' <param name="err">The string containing the output error text</param>
    ''' <param name="cond">Optional condition under which the err should be printed</param>
    ''' <param name="repCond">Optional condition under which to repair the given key</param>
    ''' <param name="newVal">The value to replace the error value</param>
    ''' <param name="repairVal">The error value</param>
    Private Sub fullKeyErr(key As iniKey, err As String, Optional cond As Boolean = True, Optional repCond As Boolean = False, Optional ByRef repairVal As String = "", Optional newVal As String = "")
        If cond Then customErr(key.lineNumber, err, {$"Key: {key.toString}"})
        fixStr(cond And repCond, repairVal, newVal)
    End Sub

    ''' <summary>
    ''' Prints arbitrarily defined errors without a precondition
    ''' </summary>
    ''' <param name="lineCount"></param>
    ''' <param name="err"></param>
    ''' <param name="lines"></param>
    Private Sub customErr(lineCount As Integer, err As String, lines As String())
        cwl($"Line: {lineCount} - Error: {err}")
        lines.ToList.ForEach(Sub(errStr As String) cwl(errStr))
        cwl()
        errorsFound += 1
    End Sub

    ''' <summary>
    ''' Replace a given string with a new value if the fix condition is met. 
    ''' </summary>
    ''' <param name="param">The condition under which the string should be replaced</param>
    ''' <param name="currentValue">The current value of the given string</param>
    ''' <param name="newValue">The replacement value for the given string</param>
    Private Sub fixStr(param As Boolean, ByRef currentValue As String, newValue As String)
        If param Then currentValue = newValue
    End Sub
End Module