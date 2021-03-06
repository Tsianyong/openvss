// ksgr	 IMPORTANT: READ BEFORE DOWNLOADING, COPYING, INSTALLING OR USING. 
// ejuz	
// xkfo	 By downloading, copying, installing or using the software you agree to this license.
// zcfd	 If you do not agree to this license, do not download, install,
// gwfz	 copy or use the software.
// aqrs	
// eixo	                          License Agreement
// ewpj	         For OpenVSS - Open Source Video Surveillance System
// cslz	
// lmvr	Copyright (C) 2007-2010, Prince of Songkla University, All rights reserved.
// kbrf	
// xwrw	Third party copyrights are property of their respective owners.
// juig	
// gwst	Redistribution and use in source and binary forms, with or without modification,
// aomw	are permitted provided that the following conditions are met:
// lgua	
// vvlh	  * Redistribution's of source code must retain the above copyright notice,
// rfst	    this list of conditions and the following disclaimer.
// aooz	
// zmvi	  * Redistribution's in binary form must reproduce the above copyright notice,
// qaek	    this list of conditions and the following disclaimer in the documentation
// ojcf	    and/or other materials provided with the distribution.
// zryx	
// azfe	  * Neither the name of the copyright holders nor the names of its contributors 
// hqjb	    may not be used to endorse or promote products derived from this software 
// kkfd	    without specific prior written permission.
// cofs	
// itun	This software is provided by the copyright holders and contributors "as is" and
// sdfk	any express or implied warranties, including, but not limited to, the implied
// bkmm	warranties of merchantability and fitness for a particular purpose are disclaimed.
// tghz	In no event shall the Prince of Songkla University or contributors be liable 
// hutb	for any direct, indirect, incidental, special, exemplary, or consequential damages
// ijri	(including, but not limited to, procurement of substitute goods or services;
// ctyg	loss of use, data, or profits; or business interruption) however caused
// vbha	and on any theory of liability, whether in contract, strict liability,
// djxg	or tort (including negligence or otherwise) arising in any way out of
// wkew	the use of this software, even if advised of the possibility of such damage.
// tudx	
// dxid	Intelligent Systems Laboratory (iSys Lab)
// dgwh	Faculty of Engineering, Prince of Songkla University, THAILAND
// ueky	Project leader by Nikom SUVONVORN
// blkz	Project website at http://code.google.com/p/openvss/

using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Threading;
using System.Drawing;
using MySql.Data.MySqlClient;
using System.Collections;
using System.Runtime.CompilerServices;
using Vs.Core.Image;
using System.Globalization;
using NLog; 

namespace Vs.Core.EventAlert
{
    /// <summary>
    /// 
    /// </summary>
    public class VsEventGenerator : IDisposable
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        // database
        string strConnect = null;
        private string tableName;

        // Database host
        private string  dbHost;
        private string  dbUser;
        private string  dbPasswd;
        private string  dbDatabase;

        private long syncTimer = 200;

        MySqlConnection connectSQL = null; 

        // buffer
        protected Queue eventBuffer = null;

        // timer
        private System.Threading.Timer timer = null;
        TimerCallback tcallback = null;

        // -----------------------------------------------------------------------------------------------------------------------
        // Database host properties
        // -----------------------------------------------------------------------------------------------------------------------
        // Database host property
        public string DataHost
        {
            get { return dbHost; }
            set { dbHost = value; }
        }

        // Data user property
        public string DataUser
        {
            get { return dbUser; }
            set { dbUser = value; }
        }

        // Data password property
        public string DataPasswd
        {
            get { return dbPasswd; }
            set { dbPasswd = value; }
        }
        
        // Data database property
        public string DataDatabase
        {
            get { return dbDatabase; }
            set { dbDatabase = value; }
        }
        // TableName property
        public string TableName
        {
            get { return tableName; }
            set { tableName = value; }
        }

        // Constructor
        public VsEventGenerator(long syncTimer, string dbHost, string dbUser, string dbPasswd, string dbDatabase)
        {
            try
            {
                this.syncTimer = syncTimer;

                // data host
                this.dbHost = dbHost;
                this.dbUser = dbUser;
                this.dbPasswd = dbPasswd;
                this.dbDatabase = dbDatabase;

                // synchronized queue
                eventBuffer = Queue.Synchronized(new Queue());

                // timer
                tcallback = new TimerCallback(process_NewFrame);
                // define the dueTime and period 
                long dTime = 1000;           // wait before the first tick (in ms) 
                // instantiate the Timer object 
                timer = new Timer(tcallback, null, dTime, syncTimer);
            }
            catch (Exception err)
            {
                logger.Log(LogLevel.Error, err.Message + " " + err.Source + " " + err.StackTrace);
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            try
            {
                if (disposing)
                {
                    timer.Dispose();
                    timer = null;

                    eventBuffer.Clear();
                    eventBuffer = null;

                    if (connectSQL != null)
                    {
                        connectSQL.Dispose();
                        connectSQL = null;
                    }
                }
            }
            catch (Exception err)
            {
                logger.Log(LogLevel.Error, err.Message + " " + err.Source + " " + err.StackTrace);
            }
        }

        // On new frame ready
        //[MethodImpl(MethodImplOptions.Synchronized)]
        public void FrameIn(object sender, VsMotionEventArgs e)
        {
            try
            {
                /*
                if (eventBuffer.Count > 1000 / syncTimer)
                {
                    VsMotion rm = (VsMotion)eventBuffer.Dequeue();
                    rm.Dispose(); rm = null;
                    logger.Log(LogLevel.Warn, DateTime.Now.ToString() + "; frame removed from EventAlert");
                }*/

                VsMotion img = (VsMotion)e.Motion.Clone();
                eventBuffer.Enqueue(img);
            }
            catch (Exception err)
            {
                logger.Log(LogLevel.Error, err.Message + " " + err.Source + " " + err.StackTrace);
            }
        }

        // Process new frame
        [MethodImpl(MethodImplOptions.Synchronized)]
        private void process_NewFrame(object stateInfo)
        {
            VsMotion lastMotion = null;

            Thread.CurrentThread.Priority = ThreadPriority.Lowest;

            try
            {
                // get new one
                if (eventBuffer.Count > 0)
                {
                    lastMotion = (VsMotion)eventBuffer.Dequeue();
                }

                if (lastMotion != null)
                {
                    try
                    {
                        strConnect = String.Format("server={0};user id={1}; password={2}; database={3}; pooling=false",
                            DataHost, DataUser, DataPasswd, DataDatabase);

                        connectSQL = new MySqlConnection(strConnect);
                        connectSQL.Open();
                    }
                    catch (MySqlException err)
                    {
                        logger.Log(LogLevel.Error, err.Message + " " + err.Source + " " + err.StackTrace);

                        if (connectSQL != null)
                        {
                            connectSQL.Dispose();
                            connectSQL = null;
                        }
                    }
                    
                    if (connectSQL != null)
                    {
                        MySqlCommand cmdSQL;

                        TableCheck(connectSQL);
                        lastMotion.TableName = TableName;

                        cmdSQL = new MySqlCommand(lastMotion.Sql, connectSQL);

                        try
                        {
                            cmdSQL.ExecuteNonQuery();
                        }
                        catch (MySqlException err)
                        {
                            logger.Log(LogLevel.Error, err.Message + " " + err.Source + " " + err.StackTrace);
                        }

                        cmdSQL.Dispose();
                    }

                    if (connectSQL != null)
                    {
                        try
                        {
                            connectSQL.Close();
                            connectSQL.Dispose();
                            connectSQL = null;
                        }
                        catch (Exception err)
                        {
                            logger.Log(LogLevel.Error, err.Message + " " + err.Source + " " + err.StackTrace); ;
                        }
                    }
                }
            }
            catch (Exception err)
            {
                if (connectSQL != null)
                {
                    connectSQL.Dispose();
                    connectSQL = null;
                }

                logger.Log(LogLevel.Error, err.Message + " " + err.Source + " " + err.StackTrace);
            }
        }

        private void TableCheck(MySqlConnection connectSQL)
        {
            string tableName;
            DateTime dateNow = DateTime.Now;

            tableName = string.Format("event_{0}_{1}",
                dateNow.Month.ToString(),
                dateNow.Year.ToString());

            if (tableName != this.tableName)
            {
                this.tableName = tableName;
                TableCreate(connectSQL);
            }
        }

        private void TableCreate(MySqlConnection connectSQL)
        {
            MySqlCommand cmdSQL;

            string strSQL = "CREATE TABLE IF NOT EXISTS  `vsa-main`.`" + TableName + "` (" +
                    "  `m_id` int(10) unsigned NOT NULL auto_increment," +
                    "  `m_ip_camera` varchar(45) NOT NULL," +
                    "  `m_ip_processor` varchar(45) NOT NULL," +
                    "  `m_date_start` datetime NOT NULL," +
                    "  `m_event` varchar(45) NOT NULL," +
                    "  PRIMARY KEY  (`m_id`)" +
                    ") ENGINE=InnoDB AUTO_INCREMENT=10 DEFAULT CHARSET=latin1;";

            cmdSQL = new MySqlCommand(strSQL, connectSQL);
            try
            {
                cmdSQL.ExecuteNonQuery();
            }
            catch (MySqlException ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
                cmdSQL.Dispose();
            }
        }

    }
}
