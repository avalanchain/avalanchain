﻿(function () {
    'use strict';

    angular.module('avalanchain').filter('pagination', function () {
        return function (input, start) {
            if (input) {
                start = +start; //parse to int
                return input.slice(start);
            }
            return [];
        }
    });

    angular
        .module('avalanchain')
        .factory('dataservice', dataservice);

    dataservice.$inject = ['$http', '$q', 'logger'];
    /* @ngInject */


    function dataservice($http, $q, logger) {
        var service = {
            getData: getData,
            getAccounts: getAccounts,
            getYData: getYData,
            addCluster: addCluster,
            addNode: addNode,
            addStream: addStream,
            deleteCluster: deleteCluster,
            clearAllProcesses: clearAllProcesses,
        };

        return service;

        function getAccounts() {
            var accounts = [];

            for (var i = 0; i < 20; i++) {
                accounts.push({
                    status: i % 3 !== 0 ? 'active' : 'unactive',
                    id: createGuid(),
                    transactions:[1,2,3],
                    balance:980+i
                });
            }

            return accounts;
        }
        function getYData() {
            var url = "http://query.yahooapis.com/v1/public/yql";
            var symbol = '"EURUSD","USDEUR", "USDJPY", "USDGBP", "USDAUD", "USDCHF", "USDSEK", "USDNOK", "USDRUB", "USDTRY", "USDBRL", "USDCAD", "USDCNY", "USDHKD", "USDINR", "USDKRW", "USDMXN", "USDNZD", "USDSGD", "USDZAR"';
            var data = encodeURIComponent("select * from yahoo.finance.xchange where pair in (symbol)");
            data = data.replace("symbol", symbol);
            //var data = "select * from yahoo.finance.xchange where pair in (" + symbol + ")";
            /*
            Build the string to use with with $http get to retrieve JSON data from Yahoo Finance API
            Required format is:
            http://query.yahooapis.com/v1/public/yql?q=select%20*%20from%20yahoo.finance.quotes%20where%20symbol%20in%20('aapl')&format=json&diagnostics=true&env=http://datatables.org/alltables.env
            */
            var str1 = url.concat("?q=", data);
            str1 = str1.concat("&format=json&env=store://datatables.org/alltableswithkeys");//http://datatables.org/alltables.env

            var res;
            return $http.get(str1)
                .success(function (data, status, headers, config) {
                    console.log("success data, status=" + JSON.stringify(data) + status);
                    if (data.query.results == null) {
                        console.log("No Valid Results could be Returned!!");
                    }
                    else {
                        var res = data.query.results;
                    }
                })

            .error(function (data, status, headers, config) {
                var err = status + ", " + data;
                //$scope.result = "Request failed: " + err;
                    return "error";
                });
            return $q.when(res);
            
        }

        function addCluster() {

        }

        function addNode() {

        }

        function addStream() {

        }

        function deleteCluster() {

        }
        function clearAllProcesses() {

        }

        function createGuid() {
            // http://www.ietf.org/rfc/rfc4122.txt
            var s = [];
            var hexDigits = "0123456789abcdef";
            for (var i = 0; i < 36; i++) {
                s[i] = hexDigits.substr(Math.floor(Math.random() * 0x10), 1);
            }
            s[14] = "4";  // bits 12-15 of the time_hi_and_version field to 0010
            s[19] = hexDigits.substr((s[19] & 0x3) | 0x8, 1);  // bits 6-7 of the clock_seq_hi_and_reserved to 01
            s[8] = s[13] = s[18] = s[23] = "-";

            var uuid = s.join("");
            return uuid;
        }

        function getData() {
            var data = {
                clusters: [
                {
                    id: '1',
                    nodes: [
                            {
                                id: '1',
                                streams: [
                                    {
                                        id: '1',
                                        data: 3000
                                    },
                                    {
                                        id: '2',
                                        data: 4000
                                    }
                                ]
                            },
                            {
                                id: '2',
                                streams: [
                                    {
                                        id: '1',
                                        data: 3000
                                    },
                                    {
                                        id: '2',
                                        data: 4000
                                    },
                                    {
                                        id: '3',
                                        data: 5000
                                    }
                                ]
                            }
                    ],
                    streams: 5
                },
                {
                    id: '2',
                    nodes: [
                    {
                        id: '1',
                        streams: [
                            {
                                id: '1',
                                data: 3300
                            },
                            {
                                id: '2',
                                data: 4600
                            }
                        ]
                    }
                    ]
                }
                ]
            };
            return data;
            //function success(response) {
            //    return response.result;
            //}

            function fail(e) {
                //var msg = 'XHR Failed for getData';
                //logger.error(msg);
                //return exception.catcher(msg)(e);
            }
        }
    }
})();