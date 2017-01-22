﻿
Imports System.Net.Sockets
Imports System.IO
Imports System.Windows.Forms

Module modClientTCP
    Private PlayerBuffer As ByteBuffer = New ByteBuffer
    Public PlayerSocket As TcpClient
    Private SckConnecting As Boolean
    Private SckConnected As Boolean
    Private myStream As NetworkStream
    Private myReader As StreamReader
    Private myWriter As StreamWriter
    Private asyncBuff As Byte()
    Private asyncBuffs As New List(Of Byte())
    Public shouldHandleData As Boolean
    Public servdown as boolean = False
    Public Sub Connect()
        If Not PlayerSocket Is Nothing Then
            Try
                If PlayerSocket.Connected Or SckConnecting Then Exit Sub
                PlayerSocket.Close()
                PlayerSocket = Nothing
            Catch ex As Exception

            End Try
        End If
        PlayerSocket = New TcpClient()
        PlayerSocket.ReceiveBufferSize = 4096
        PlayerSocket.SendBufferSize = 4096
        PlayerSocket.NoDelay = False
        ReDim asyncBuff(8192)
        PlayerSocket.BeginConnect(Options.IP, Options.Port, New AsyncCallback(AddressOf connectCallback), PlayerSocket)
        SckConnecting = True
    End Sub

    Sub connectCallback(asyncConnect As IAsyncResult)
        Try
            PlayerSocket.EndConnect(asyncConnect)
            If (PlayerSocket.Connected = False) Then
                SckConnecting = False
                SckConnected = False
                Exit Sub
            Else
                PlayerSocket.NoDelay = True
                myStream = PlayerSocket.GetStream()
                myStream.BeginRead(asyncBuff, 0, 8192, AddressOf OnReceive, Nothing)
                SckConnected = True
                SckConnecting = False
            End If
        Catch ex As Exception
            SckConnecting = False
            SckConnected = False
        End Try
    End Sub

    Sub OnReceive(ar As IAsyncResult)
        Try
            Dim byteAmt As Integer = myStream.EndRead(ar)
            Dim myBytes() As Byte
            ReDim myBytes(byteAmt - 1)
            Buffer.BlockCopy(asyncBuff, 0, myBytes, 0, byteAmt)
            If byteAmt = 0 Then
                servdown = True
                Exit Sub
            End If
            HandleData(myBytes)
            myStream.BeginRead(asyncBuff, 0, 8192, AddressOf OnReceive, Nothing)
        Catch ex As Exception
            servdown = True
        End Try

    End Sub

    Public Function IsConnected() As Boolean
        If PlayerSocket Is Nothing Then Exit Function
        If PlayerSocket.Connected = True Then
            IsConnected = True
        Else
            IsConnected = False
        End If
        If servdown = True Then
           IsConnected = False
        End If
    End Function
    Public Sub SendData(ByVal bytes() As Byte)
        Try
            Dim buffer As ByteBuffer
            buffer = New ByteBuffer
            buffer.WriteLong(UBound(bytes) - LBound(bytes) + 1)
            buffer.WriteBytes(bytes)
            'Send data in the socket stream to the server
            myStream.Write(buffer.ToArray, 0, buffer.ToArray.Length)
            buffer = Nothing
            'writes the packet size and sends the data.....
        Catch ex As Exception
            MsgBox("Disconnected.")
            Application.Exit()
        End Try
    End Sub
    Public Sub SendNewAccount(ByVal Name As String, ByVal Password As String)
        Dim Buffer As ByteBuffer
        Buffer = New ByteBuffer
        Buffer.WriteLong(ClientPackets.CNewAccount)
        Buffer.WriteString(Name)
        Buffer.WriteString(Password)
        SendData(Buffer.ToArray)
        Buffer = Nothing
    End Sub
    Public Sub SendAddChar(ByVal Name As String, ByVal Sex As Long, ByVal ClassNum As Long, ByVal Sprite As Long)
        Dim Buffer As ByteBuffer
        Buffer = New ByteBuffer
        Buffer.WriteLong(ClientPackets.CAddChar)
        Buffer.WriteString(Name)
        Buffer.WriteLong(Sex)
        Buffer.WriteLong(ClassNum)
        Buffer.WriteLong(Sprite)
        SendData(Buffer.ToArray())
        Buffer = Nothing
    End Sub
    Public Sub SendLogin(ByVal Name As String, ByVal Password As String)
        Dim Buffer As ByteBuffer
        Buffer = New ByteBuffer
        Buffer.WriteLong(ClientPackets.CLogin)
        Buffer.WriteString(Name)
        Buffer.WriteString(Password)
        Buffer.WriteString(Application.ProductVersion)
        SendData(Buffer.ToArray())
        Buffer = Nothing
    End Sub
    Function IsPlaying(ByVal Index As Long) As Boolean

        ' if the player doesn't exist, the name will equal 0
        If Len(GetPlayerName(Index)) > 0 Then
            IsPlaying = True
        End If

    End Function
    Function GetPlayerName(ByVal Index As Long) As String
        GetPlayerName = ""
        If Index > MAX_PLAYERS Then Exit Function
        GetPlayerName = Trim$(Player(Index).Name)
    End Function
    Sub GetPing()
        Dim Buffer As ByteBuffer
        PingStart = GetTickCount()
        Buffer = New ByteBuffer
        Buffer.WriteLong(ClientPackets.CCheckPing)
        SendData(Buffer.ToArray())
        Buffer = Nothing
    End Sub
    Public Sub SendRequestEditMap()
        Dim Buffer As ByteBuffer
        Buffer = New ByteBuffer
        Buffer.WriteLong(ClientPackets.CRequestEditMap)
        SendData(Buffer.ToArray())
        Buffer = Nothing
    End Sub
    Public Sub SendMap()
        Dim X As Long
        Dim Y As Long
        Dim i As Long
        Dim data() As Byte
        Dim Buffer As ByteBuffer
        Buffer = New ByteBuffer
        CanMoveNow = False

        Buffer.WriteString(Trim$(Map.Name))
        Buffer.WriteString(Trim$(Map.Music))
        Buffer.WriteLong(Map.Moral)
        Buffer.WriteLong(Map.tileset)
        Buffer.WriteLong(Map.Up)
        Buffer.WriteLong(Map.Down)
        Buffer.WriteLong(Map.Left)
        Buffer.WriteLong(Map.Right)
        Buffer.WriteLong(Map.BootMap)
        Buffer.WriteLong(Map.BootX)
        Buffer.WriteLong(Map.BootY)
        Buffer.WriteLong(Map.MaxX)
        Buffer.WriteLong(Map.MaxY)



        For i = 1 To MAX_MAP_NPCS
            Buffer.WriteLong(Map.Npc(i))
        Next

        For X = 0 To Map.MaxX
            For Y = 0 To Map.MaxY
                Buffer.WriteLong(Map.Tile(X, Y).Data1)
                Buffer.WriteLong(Map.Tile(X, Y).Data2)
                Buffer.WriteLong(Map.Tile(X, Y).Data3)
                Buffer.WriteLong(Map.Tile(X, Y).DirBlock)
                For i = 0 To MapLayer.Layer_Count - 1
                    Buffer.WriteLong(Map.Tile(X, Y).Layer(i).tileset)
                    Buffer.WriteLong(Map.Tile(X, Y).Layer(i).X)
                    Buffer.WriteLong(Map.Tile(X, Y).Layer(i).Y)
                Next
                Buffer.WriteLong(Map.Tile(X, Y).Type)
            Next
        Next

        data = Buffer.ToArray

        Buffer = New ByteBuffer
        Buffer.WriteLong(ClientPackets.CMapData)
        Buffer.WriteBytes(Compress(data))

        SendData(Buffer.ToArray())
        Buffer = Nothing
    End Sub
    Public Sub SendPlayerMove()
        Dim Buffer As ByteBuffer
        Buffer = New ByteBuffer
        Buffer.WriteLong(ClientPackets.CPlayerMove)
        Buffer.WriteLong(GetPlayerDir(MyIndex))
        Buffer.WriteLong(Player(MyIndex).Moving)
        Buffer.WriteLong(Player(MyIndex).X)
        Buffer.WriteLong(Player(MyIndex).Y)
        SendData(Buffer.ToArray())
        Buffer = Nothing
    End Sub
    Public Sub SayMsg(ByVal text As String)
        Dim Buffer As ByteBuffer
        Buffer = New ByteBuffer
        Buffer.WriteLong(ClientPackets.CSayMsg)
        Buffer.WriteString(text)
        SendData(Buffer.ToArray())
        Buffer = Nothing
    End Sub
    Public Sub SendKick(ByVal Name As String)
        Dim Buffer As ByteBuffer
        Buffer = New ByteBuffer
        Buffer.WriteLong(ClientPackets.CKickPlayer)
        Buffer.WriteString(Name)
        SendData(Buffer.ToArray())
        Buffer = Nothing
    End Sub

    Public Sub SendBan(ByVal Name As String)
        Dim Buffer As ByteBuffer
        Buffer = New ByteBuffer
        Buffer.WriteLong(ClientPackets.CBanPlayer)
        Buffer.WriteString(Name)
        SendData(Buffer.ToArray())
        Buffer = Nothing
    End Sub
    Public Sub WarpMeTo(ByVal Name As String)
        Dim Buffer As ByteBuffer
        Buffer = New ByteBuffer
        Buffer.WriteLong(ClientPackets.CWarpMeTo)
        Buffer.WriteString(Name)
        SendData(Buffer.ToArray())
        Buffer = Nothing
    End Sub

    Public Sub WarpToMe(ByVal Name As String)
        Dim Buffer As ByteBuffer
        Buffer = New ByteBuffer
        Buffer.WriteLong(ClientPackets.CWarpToMe)
        Buffer.WriteString(Name)
        SendData(Buffer.ToArray())
        Buffer = Nothing
    End Sub
    Public Sub WarpTo(ByVal MapNum As Long)
        Dim Buffer As ByteBuffer
        Buffer = New ByteBuffer
        Buffer.WriteLong(ClientPackets.CWarpTo)
        Buffer.WriteLong(MapNum)
        SendData(Buffer.ToArray())
        Buffer = Nothing
    End Sub
    Public Sub SendRequestLevelUp()
        Dim Buffer As ByteBuffer
        Buffer = New ByteBuffer
        Buffer.WriteLong(ClientPackets.CRequestLevelUp)
        SendData(Buffer.ToArray())
        Buffer = Nothing
    End Sub
    Sub SendSpawnItem(ByVal tmpItem As Long, ByVal tmpAmount As Long)
        Dim Buffer As ByteBuffer
        Buffer = New ByteBuffer
        Buffer.WriteLong(ClientPackets.CSpawnItem)
        Buffer.WriteLong(tmpItem)
        Buffer.WriteLong(tmpAmount)
        SendData(Buffer.ToArray())
        Buffer = Nothing
    End Sub
    Public Sub SendSetSprite(ByVal SpriteNum As Long)
        Dim Buffer As ByteBuffer
        Buffer = New ByteBuffer
        Buffer.WriteLong(ClientPackets.CSetSprite)
        Buffer.WriteLong(SpriteNum)
        SendData(Buffer.ToArray())
        Buffer = Nothing
    End Sub
    Public Sub SendSetAccess(ByVal Name As String, ByVal Access As Byte)
        Dim Buffer As ByteBuffer
        Buffer = New ByteBuffer
        Buffer.WriteLong(ClientPackets.CSetAccess)
        Buffer.WriteString(Name)
        Buffer.WriteLong(Access)
        SendData(Buffer.ToArray())
        Buffer = Nothing
    End Sub
    Sub SendRequestItems()
        Dim Buffer As ByteBuffer
        Buffer = New ByteBuffer
        Buffer.WriteLong(ClientPackets.CRequestItems)
        SendData(Buffer.ToArray())
        Buffer = Nothing
    End Sub
    Sub SendSaveItem(ByVal itemNum As Long)
        Dim Buffer As ByteBuffer
        Buffer = New ByteBuffer
        Buffer.WriteLong(ClientPackets.CSaveItem)
        Buffer.WriteLong(itemNum)
        Buffer.WriteLong(Item(itemNum).AccessReq)

        For i = 0 To Stats.stat_count - 1
            Buffer.WriteLong(Item(itemNum).Add_Stat(i))
        Next

        Buffer.WriteLong(Item(itemNum).Animation)
        Buffer.WriteLong(Item(itemNum).BindType)
        Buffer.WriteLong(Item(itemNum).ClassReq)
        Buffer.WriteLong(Item(itemNum).Data1)
        Buffer.WriteLong(Item(itemNum).Data2)
        Buffer.WriteLong(Item(itemNum).Data3)
        Buffer.WriteLong(Item(itemNum).Handed)
        Buffer.WriteLong(Item(itemNum).LevelReq)
        Buffer.WriteLong(Item(itemNum).Mastery)
        Buffer.WriteString(Item(itemNum).Name)
        Buffer.WriteLong(Item(itemNum).Paperdoll)
        Buffer.WriteLong(Item(itemNum).Pic)
        Buffer.WriteLong(Item(itemNum).Price)
        Buffer.WriteLong(Item(itemNum).Rarity)
        Buffer.WriteLong(Item(itemNum).Speed)

        For i = 0 To Stats.stat_count - 1
            Buffer.WriteLong(Item(itemNum).Stat_Req(i))
        Next

        Buffer.WriteLong(Item(itemNum).Type)

        SendData(Buffer.ToArray())
        Buffer = Nothing
    End Sub
    Public Sub SendRequestEditItem()
        Dim Buffer As ByteBuffer
        Buffer = New ByteBuffer
        Buffer.WriteLong(ClientPackets.CRequestEditItem)
        SendData(Buffer.ToArray())
        Buffer = Nothing
    End Sub
    Public Sub SendPlayerDir()
        Dim Buffer As ByteBuffer
        Buffer = New ByteBuffer
        Buffer.WriteLong(ClientPackets.CPlayerDir)
        Buffer.WriteLong(GetPlayerDir(MyIndex))
        SendData(Buffer.ToArray())
        Buffer = Nothing
    End Sub

    Public Sub SendPlayerRequestNewMap()
        Dim Buffer As ByteBuffer
        Buffer = New ByteBuffer
        Buffer.WriteLong(ClientPackets.CRequestNewMap)
        Buffer.WriteLong(GetPlayerDir(MyIndex))
        SendData(Buffer.ToArray())
        Buffer = Nothing
    End Sub
    Public Sub SendRequestEditResource()
        Dim Buffer As ByteBuffer
        Buffer = New ByteBuffer
        Buffer.WriteLong(ClientPackets.CRequestEditResource)
        SendData(Buffer.ToArray())
        Buffer = Nothing
    End Sub
    Sub SendRequestResources()
        Dim Buffer As ByteBuffer
        Buffer = New ByteBuffer
        Buffer.WriteLong(ClientPackets.CRequestResources)
        SendData(Buffer.ToArray())
        Buffer = Nothing
    End Sub
    Public Sub SendSaveResource(ByVal ResourceNum As Long)
        Dim Buffer As ByteBuffer
        Buffer = New ByteBuffer
        Buffer.WriteLong(ClientPackets.CSaveResource)
        Buffer.WriteLong(ResourceNum)
        Buffer.WriteLong(Resource(ResourceNum).Animation)
        Buffer.WriteString(Resource(ResourceNum).EmptyMessage)
        Buffer.WriteLong(Resource(ResourceNum).ExhaustedImage)
        Buffer.WriteLong(Resource(ResourceNum).Health)
        Buffer.WriteLong(Resource(ResourceNum).ItemReward)
        Buffer.WriteString(Resource(ResourceNum).Name)
        Buffer.WriteLong(Resource(ResourceNum).ResourceImage)
        Buffer.WriteLong(Resource(ResourceNum).ResourceType)
        Buffer.WriteLong(Resource(ResourceNum).RespawnTime)
        Buffer.WriteString(Resource(ResourceNum).SuccessMessage)
        Buffer.WriteLong(Resource(ResourceNum).ToolRequired)
        Buffer.WriteLong(Resource(ResourceNum).Walkthrough)
        SendData(Buffer.ToArray())
        Buffer = Nothing
    End Sub
    Public Sub SendRequestEditNpc()
        Dim Buffer As ByteBuffer
        Buffer = New ByteBuffer
        Buffer.WriteLong(ClientPackets.CRequestEditNpc)
        SendData(Buffer.ToArray())
        Buffer = Nothing
    End Sub
    Public Sub SendSaveNpc(ByVal NpcNum As Long)
        Dim Buffer As ByteBuffer
        Buffer = New ByteBuffer
        Buffer.WriteLong(ClientPackets.CSaveNpc)
        Buffer.WriteLong(NpcNum)

        Buffer.WriteLong(Npc(NpcNum).Animation)
        Buffer.WriteString(Npc(NpcNum).AttackSay)
        Buffer.WriteLong(Npc(NpcNum).Behaviour)
        Buffer.WriteLong(Npc(NpcNum).DropChance)
        Buffer.WriteLong(Npc(NpcNum).DropItem)
        Buffer.WriteLong(Npc(NpcNum).DropItemValue)
        Buffer.WriteLong(Npc(NpcNum).EXP)
        Buffer.WriteLong(Npc(NpcNum).faction)
        Buffer.WriteLong(Npc(NpcNum).HP)
        Buffer.WriteString(Npc(NpcNum).Name)
        Buffer.WriteLong(Npc(NpcNum).Range)
        Buffer.WriteLong(Npc(NpcNum).SpawnSecs)
        Buffer.WriteLong(Npc(NpcNum).Sprite)

        For i = 0 To Stats.stat_count - 1
            Buffer.WriteLong(Npc(NpcNum).Stat(i))
        Next

        SendData(Buffer.ToArray())
        Buffer = Nothing
    End Sub
    Sub SendRequestNPCS()
        Dim Buffer As ByteBuffer
        Buffer = New ByteBuffer
        Buffer.WriteLong(ClientPackets.CRequestNPCS)
        SendData(Buffer.ToArray())
        Buffer = Nothing
    End Sub
    Public Sub SendRequestEditSpell()
        Dim Buffer As ByteBuffer
        Buffer = New ByteBuffer
        Buffer.WriteLong(ClientPackets.CRequestEditSpell)
        SendData(Buffer.ToArray())
        Buffer = Nothing
    End Sub
    Sub SendRequestSpells()
        Dim Buffer As ByteBuffer
        Buffer = New ByteBuffer
        Buffer.WriteLong(ClientPackets.CRequestSpells)
        SendData(Buffer.ToArray())
        Buffer = Nothing
    End Sub
    Public Sub SendSaveSpell(ByVal spellnum As Long)
        Dim Buffer As ByteBuffer
        Buffer = New ByteBuffer

        Buffer.WriteLong(ClientPackets.CSaveSpell)
        Buffer.WriteLong(spellnum)

        Buffer.WriteLong(Spell(spellnum).AccessReq)
        Buffer.WriteLong(Spell(spellnum).AoE)
        Buffer.WriteLong(Spell(spellnum).CastAnim)
        Buffer.WriteLong(Spell(spellnum).CastTime)
        Buffer.WriteLong(Spell(spellnum).CDTime)
        Buffer.WriteLong(Spell(spellnum).ClassReq)
        Buffer.WriteLong(Spell(spellnum).Dir)
        Buffer.WriteLong(Spell(spellnum).Duration)
        Buffer.WriteLong(Spell(spellnum).Icon)
        Buffer.WriteLong(Spell(spellnum).Interval)
        Buffer.WriteLong(Spell(spellnum).IsAoE)
        Buffer.WriteLong(Spell(spellnum).LevelReq)
        Buffer.WriteLong(Spell(spellnum).Map)
        Buffer.WriteLong(Spell(spellnum).MPCost)
        Buffer.WriteString(Spell(spellnum).Name)
        Buffer.WriteLong(Spell(spellnum).Range)
        Buffer.WriteLong(Spell(spellnum).SpellAnim)
        Buffer.WriteLong(Spell(spellnum).StunDuration)
        Buffer.WriteLong(Spell(spellnum).Type)
        Buffer.WriteLong(Spell(spellnum).Vital)
        Buffer.WriteLong(Spell(spellnum).X)
        Buffer.WriteLong(Spell(spellnum).Y)

        SendData(Buffer.ToArray())

        Buffer = Nothing
    End Sub

    Sub SendRequestShops()
        Dim Buffer As ByteBuffer
        Buffer = New ByteBuffer
        Buffer.WriteLong(ClientPackets.CRequestShops)
        SendData(Buffer.ToArray())
        Buffer = Nothing
    End Sub
    Public Sub SendSaveShop(ByVal shopnum As Long)
        Dim Buffer As ByteBuffer
        Buffer = New ByteBuffer
        Buffer.WriteLong(ClientPackets.CSaveShop)
        Buffer.WriteLong(shopnum)

        Buffer.WriteLong(Shop(shopnum).BuyRate)
        Buffer.WriteString(Shop(shopnum).Name)

        For i = 0 To MAX_TRADES
            Buffer.WriteLong(Shop(shopnum).TradeItem(i).CostItem)
            Buffer.WriteLong(Shop(shopnum).TradeItem(i).CostValue)
            Buffer.WriteLong(Shop(shopnum).TradeItem(i).Item)
            Buffer.WriteLong(Shop(shopnum).TradeItem(i).ItemValue)
        Next

        SendData(Buffer.ToArray())
        Buffer = Nothing

    End Sub
    Public Sub SendRequestEditShop()
        Dim Buffer As ByteBuffer
        Buffer = New ByteBuffer
        Buffer.WriteLong(ClientPackets.CRequestEditShop)
        SendData(Buffer.ToArray())
        Buffer = Nothing
    End Sub
    Public Sub SendSaveAnimation(ByVal Animationnum As Long)
        Dim Buffer As ByteBuffer
        Buffer = New ByteBuffer
        Buffer.WriteLong(ClientPackets.CSaveAnimation)
        Buffer.WriteLong(Animationnum)

        For i = 0 To UBound(Animation(Animationnum).Frames)
            Buffer.WriteLong(Animation(Animationnum).Frames(i))
        Next

        For i = 0 To UBound(Animation(Animationnum).LoopCount)
            Buffer.WriteLong(Animation(Animationnum).LoopCount(i))
        Next

        For i = 0 To UBound(Animation(Animationnum).looptime)
            Buffer.WriteLong(Animation(Animationnum).looptime(i))
        Next

        Buffer.WriteString(Animation(Animationnum).Name)

        For i = 0 To UBound(Animation(Animationnum).Sprite)
            Buffer.WriteLong(Animation(Animationnum).Sprite(i))
        Next


        SendData(Buffer.ToArray())
        Buffer = Nothing
    End Sub

    Sub SendRequestAnimations()
        Dim Buffer As ByteBuffer
        Buffer = New ByteBuffer
        Buffer.WriteLong(ClientPackets.CRequestAnimations)
        SendData(Buffer.ToArray())
        Buffer = Nothing
    End Sub
    Public Sub SendRequestEditAnimation()
        Dim Buffer As ByteBuffer
        Buffer = New ByteBuffer
        Buffer.WriteLong(ClientPackets.CRequestEditAnimation)
        SendData(Buffer.ToArray())
        Buffer = Nothing
    End Sub
    Public Sub SendBanDestroy()
        Dim Buffer As ByteBuffer
        Buffer = New ByteBuffer
        Buffer.WriteLong(ClientPackets.CBanDestroy)
        SendData(Buffer.ToArray())
        Buffer = Nothing
    End Sub
    Public Sub SendMapRespawn()
        Dim Buffer As ByteBuffer
        Buffer = New ByteBuffer
        Buffer.WriteLong(ClientPackets.CMapRespawn)
        SendData(Buffer.ToArray())
        Buffer = Nothing
    End Sub
    Sub SendTrainStat(ByVal StatNum As Byte)
        Dim Buffer As ByteBuffer
        Buffer = New ByteBuffer
        Buffer.WriteLong(ClientPackets.CTrainStat)
        Buffer.WriteLong(StatNum)
        SendData(Buffer.ToArray())
        Buffer = Nothing
    End Sub
    Sub SendRequestPlayerData()
        Dim Buffer As ByteBuffer
        Buffer = New ByteBuffer
        Buffer.WriteLong(ClientPackets.CRequestPlayerData)
        SendData(Buffer.ToArray())
        Buffer = Nothing
    End Sub
    Public Sub BroadcastMsg(ByVal text As String)
        Dim Buffer As ByteBuffer
        Buffer = New ByteBuffer
        Buffer.WriteLong(ClientPackets.CBroadcastMsg)
        Buffer.WriteString(text)
        SendData(Buffer.ToArray())
        Buffer = Nothing
    End Sub
    Public Sub EmoteMsg(ByVal text As String)
        Dim Buffer As ByteBuffer
        Buffer = New ByteBuffer
        Buffer.WriteLong(ClientPackets.CEmoteMsg)
        Buffer.WriteString(text)
        SendData(Buffer.ToArray())
        Buffer = Nothing
    End Sub
    Public Sub PlayerMsg(ByVal text As String, ByVal MsgTo As String)
        Dim Buffer As ByteBuffer
        Buffer = New ByteBuffer
        Buffer.WriteLong(ClientPackets.CPlayerMsg)
        Buffer.WriteString(MsgTo)
        Buffer.WriteString(text)
        SendData(Buffer.ToArray())
        Buffer = Nothing
    End Sub
    Public Sub SendWhosOnline()
        Dim Buffer As ByteBuffer
        Buffer = New ByteBuffer
        Buffer.WriteLong(ClientPackets.CWhosOnline)
        SendData(Buffer.ToArray())
        Buffer = Nothing
    End Sub
    Public Sub SendMOTDChange(ByVal MOTD As String)
        Dim Buffer As ByteBuffer
        Buffer = New ByteBuffer
        Buffer.WriteLong(ClientPackets.CSetMotd)
        Buffer.WriteString(MOTD)
        SendData(Buffer.ToArray())
        Buffer = Nothing
    End Sub
    Public Sub SendBanList()
        Dim Buffer As ByteBuffer
        Buffer = New ByteBuffer
        Buffer.WriteLong(ClientPackets.CBanList)
        SendData(Buffer.ToArray())
        Buffer = Nothing
    End Sub
    Sub SendChangeInvSlots(ByVal OldSlot As Integer, ByVal NewSlot As Integer)
        Dim Buffer As ByteBuffer
        Buffer = New ByteBuffer
        Buffer.WriteLong(ClientPackets.CSwapInvSlots)
        Buffer.WriteInteger(OldSlot)
        Buffer.WriteInteger(NewSlot)
        SendData(Buffer.ToArray())
        Buffer = Nothing
    End Sub
    Public Sub SendUseItem(ByVal InvNum As Long)
        Dim Buffer As ByteBuffer
        Buffer = New ByteBuffer
        Buffer.WriteLong(ClientPackets.CUseItem)
        Buffer.WriteLong(InvNum)
        SendData(Buffer.ToArray())
        Buffer = Nothing
    End Sub
    Public Sub SendDropItem(ByVal InvNum As Long, ByVal Amount As Long)
        Dim Buffer As ByteBuffer

        If InBank Or InShop Then Exit Sub

        ' do basic checks
        If InvNum < 1 Or InvNum > MAX_INV Then Exit Sub
        If PlayerInv(InvNum).Num < 1 Or PlayerInv(InvNum).Num > MAX_ITEMS Then Exit Sub
        If Item(GetPlayerInvItemNum(MyIndex, InvNum)).Type = ITEM_TYPE_CURRENCY Then
            If Amount < 1 Or Amount > PlayerInv(InvNum).Value Then Exit Sub
        End If

        Buffer = New ByteBuffer
        Buffer.WriteLong(ClientPackets.CMapDropItem)
        Buffer.WriteLong(InvNum)
        Buffer.WriteLong(Amount)
        SendData(Buffer.ToArray())
        Buffer = Nothing
    End Sub
    Public Sub BuyItem(ByVal shopslot As Long)
        Dim Buffer As ByteBuffer
        Buffer = New ByteBuffer
        Buffer.WriteLong(ClientPackets.CBuyItem)
        Buffer.WriteLong(shopslot)
        SendData(Buffer.ToArray())
        Buffer = Nothing
    End Sub
    Public Sub SellItem(ByVal invslot As Long)
        Dim Buffer As ByteBuffer
        Buffer = New ByteBuffer
        Buffer.WriteLong(ClientPackets.CSellItem)
        Buffer.WriteLong(invslot)
        SendData(Buffer.ToArray())
        Buffer = Nothing
    End Sub
    Public Sub DepositItem(ByVal invslot As Long, ByVal Amount As Long)
        Dim Buffer As ByteBuffer
        Buffer = New ByteBuffer
        Buffer.WriteLong(ClientPackets.CDepositItem)
        Buffer.WriteLong(invslot)
        Buffer.WriteLong(Amount)
        SendData(Buffer.ToArray())
        Buffer = Nothing
    End Sub
    Public Sub WithdrawItem(ByVal bankslot As Long, ByVal Amount As Long)
        Dim Buffer As ByteBuffer
        Buffer = New ByteBuffer
        Buffer.WriteLong(ClientPackets.CWithdrawItem)
        Buffer.WriteLong(bankslot)
        Buffer.WriteLong(Amount)
        SendData(Buffer.ToArray())
        Buffer = Nothing
    End Sub
    Public Sub ChangeBankSlots(ByVal OldSlot As Long, ByVal NewSlot As Long)
        Dim Buffer As ByteBuffer
        Buffer = New ByteBuffer
        Buffer.WriteLong(ClientPackets.CChangeBankSlots)
        Buffer.WriteLong(OldSlot)
        Buffer.WriteLong(NewSlot)
        SendData(Buffer.ToArray())
        Buffer = Nothing
    End Sub
    Public Sub CloseBank()
        Dim Buffer As ByteBuffer
        Buffer = New ByteBuffer
        Buffer.WriteLong(ClientPackets.CCloseBank)
        SendData(Buffer.ToArray())
        Buffer = Nothing
        InBank = False
        frmMainGame.pnlBank.Visible = False
    End Sub
    Sub PlayerSearch(ByVal CurX As Integer, ByVal CurY As Integer)
        Dim Buffer As ByteBuffer

        If isInBounds() Then
            Buffer = New ByteBuffer
            Buffer.WriteLong(ClientPackets.CSearch)
            Buffer.WriteLong(CurX)
            Buffer.WriteLong(CurY)
            SendData(Buffer.ToArray())
            Buffer = Nothing
        End If

    End Sub

    Sub SendTradeRequest(ByVal CurX As Integer, ByVal CurY As Integer)
        Dim Buffer As ByteBuffer
        If isInBounds() Then
            Buffer = New ByteBuffer
            Buffer.WriteLong(ClientPackets.CTradeRequest)
            Buffer.WriteLong(CurX)
            Buffer.WriteLong(CurY)
            SendData(Buffer.ToArray())
            Buffer = Nothing
        End If

    End Sub
    Public Sub AdminWarp(ByVal X As Long, ByVal Y As Long)
        Dim Buffer As ByteBuffer
        Buffer = New ByteBuffer
        Buffer.WriteLong(ClientPackets.CAdminWarp)
        Buffer.WriteLong(X)
        Buffer.WriteLong(Y)
        SendData(Buffer.ToArray())
        Buffer = Nothing
    End Sub
    Public Sub TradeItem(ByVal invslot As Long, ByVal Amount As Long)
        Dim Buffer As ByteBuffer
        Buffer = New ByteBuffer
        Buffer.WriteLong(ClientPackets.CTradeItem)
        Buffer.WriteLong(invslot)
        Buffer.WriteLong(Amount)
        SendData(Buffer.ToArray())
        Buffer = Nothing
    End Sub
    Public Sub UntradeItem(ByVal invslot As Long)
        Dim Buffer As ByteBuffer
        Buffer = New ByteBuffer
        Buffer.WriteLong(ClientPackets.CUntradeItem)
        Buffer.WriteLong(invslot)
        SendData(Buffer.ToArray())
        Buffer = Nothing
    End Sub
    Public Sub AcceptTrade()
        Dim Buffer As ByteBuffer
        Buffer = New ByteBuffer
        Buffer.WriteLong(ClientPackets.CAcceptTrade)
        SendData(Buffer.ToArray())
        Buffer = Nothing
    End Sub

    Public Sub DeclineTrade()
        Dim Buffer As ByteBuffer
        Buffer = New ByteBuffer
        Buffer.WriteLong(ClientPackets.CDeclineTrade)
        SendData(Buffer.ToArray())
        Buffer = Nothing
    End Sub
    Public Sub SendPartyRequest(ByVal Name As String)
        Dim Buffer As ByteBuffer
        Buffer = New ByteBuffer
        Buffer.WriteLong(ClientPackets.CParty)
        Buffer.WriteString(Name)
        SendData(Buffer.ToArray())
        Buffer = Nothing
    End Sub

    Public Sub SendJoinParty()
        Dim Buffer As ByteBuffer
        Buffer = New ByteBuffer
        Buffer.WriteLong(ClientPackets.CJoinParty)
        SendData(Buffer.ToArray())
        Buffer = Nothing
    End Sub

    Public Sub SendLeaveParty()
        Dim Buffer As ByteBuffer
        Buffer = New ByteBuffer
        Buffer.WriteLong(ClientPackets.CLeaveParty)
        SendData(Buffer.ToArray())
        Buffer = Nothing
    End Sub
    Sub SendUnequip(ByVal EqNum As Long)
        Dim Buffer As ByteBuffer
        Buffer = New ByteBuffer
        Buffer.WriteLong(ClientPackets.CUnequip)
        Buffer.WriteLong(EqNum)
        SendData(Buffer.ToArray())
        Buffer = Nothing
    End Sub
    Public Sub ForgetSpell(ByVal spellslot As Long)
        Dim Buffer As ByteBuffer

        ' Check for subscript out of range
        If spellslot < 1 Or spellslot > MAX_PLAYER_SPELLS Then
            Exit Sub
        End If

        ' dont let them forget a spell which is in CD
        If SpellCD(spellslot) > 0 Then
            AddText("Cannot forget a spell which is cooling down!")
            Exit Sub
        End If

        ' dont let them forget a spell which is buffered
        If SpellBuffer = spellslot Then
            AddText("Cannot forget a spell which you are casting!")
            Exit Sub
        End If

        If PlayerSpells(spellslot) > 0 Then
            Buffer = New ByteBuffer
            Buffer.WriteLong(ClientPackets.CForgetSpell)
            Buffer.WriteLong(spellslot)
            SendData(Buffer.ToArray())
            Buffer = Nothing
        Else
            AddText("No spell here.")
        End If
    End Sub
End Module
