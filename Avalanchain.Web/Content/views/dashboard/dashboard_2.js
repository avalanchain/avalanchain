(function () {
    'use strict';
    var controllerId = 'Dashboard_2';
    angular.module('avalanchain').controller(controllerId, ['common', 'dataservice','$scope', dashboard_2]);

    function dashboard_2(common, dataservice, $scope) {
        var getLogFn = common.logger.getLogFn;
        var log = getLogFn(controllerId);
        var vm = this;
        vm.info = 'Dashboard 2';
        vm.helloText = 'Welcome in Avalanchain';
        vm.descriptionText = 'CASCADING REACTIVE BLOCKCHAINS';
        var symbol = ["USD", "EUR", "JPY", "GBP", "AUD", "CHF", "SEK", "NOK", "RUB", "TRY", "BRL", "CAD", "CNY", "HKD", "INR", "KRW", "MXN", "NZD", "SGD", "ZAR"];
        var percentage = [45, 17, 12, 6, 4, 3, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0.5, 0.5];
        $scope.currlist = [];
        $scope.quoka = 0;
        for (var i = 0; i < symbol.length; i++) {
            var color = '#ed5565';
            if (i > 0) color = getRandomColor();
            $scope.currlist.push({
                id: symbol[i],
                value: percentage[i],
                color: color,

            });
        }
        
        //vm.datayahoo = dataservice.getYData();
        function getRandomColor() {
            var letters = '0123456789ABCDEF'.split('');
            var color = '#';
            for (var i = 0; i < 6; i++) {
                color += letters[Math.floor(Math.random() * 16)];
            }
            return color;
        }

        
        
        //function start() {
        //    signalrdata.getDevices(vm);
        //}
        //setInterval(function updateData() {
        //    $scope.$storage.data.messages += 3;
        //    $scope.$storage.data.tweets += 3;
        //    //$scope.$digest();
        //}, 5000);

         setInterval(function updateRandom() {
             //$scope.flotLineAreaData[0].data = getRandomData();//$scope.quoka;
            //$scope.$digest();
            ////plot.setData(series);lineAreaData
             ////plot.draw();
             getMessageCount();
         }, 3000);
         var lineAreaOptions = {
             series: {
                 lines: {
                     show: true,
                     lineWidth: 2,
                     fill: true,
                     fillColor: {
                         colors: [
                             {
                                 opacity: 0.7
                             },
                             {
                                 opacity: 0.5
                             }
                         ]
                     }
                 }
             },
             xaxis: {
                 tickDecimals: 0
             },
             yaxis: {
                 min:37.27
             },
             //yaxis: { min: 37.27, ticks: [37.275, 37.300, 37.325, 37.350, 37.375, 37.400, 37.425, 37.450, 37.475, 37.500, 37.525] },
             colors: ["#ed5565"],
             grid: {
                 color: "#999999",
                 hoverable: true,
                 clickable: true,
                 tickColor: "#D4D4D4",
                 borderWidth: 0
             },
             legend: {
                 show: false
             },
             tooltip: true,
             tooltipOpts: {
                 content: "x: %x, y: %y"
             }
         };
        var dataF = [
            [1, 37.275],
            [2, 37.28],
            [3, 37.295],
            [4, 37.27],
            [5, 37.285],
            [6, 37.27],
            [7, 37.295],
            [8, 37.27],
            [9, 37.285],
            [10, 37.275],
            [11, 37.29],
            [12, 37.295],
            [13, 37.29]
        ];
         var lineAreaData = [
             {
                 label: "line",
                 data: dataF
             }
         ];
         $scope.flotLineAreaOptions = lineAreaOptions;
         $scope.flotLineAreaData = lineAreaData;
         var container = $("#flot-line-chart-moving");
         var maximum = container.outerWidth() / 2 || 300;

         function getRandomData() {

             if (dataF.length) {
                 dataF = dataF.slice(1);
                 }

             while (dataF.length < maximum) {
                 var previous = dataF.length ? dataF[dataF.length - 1] : 38;
                     var y = previous + Math.random() * 10 - 5;
                     dataF.push(y < 37 ? 37.1 : y > 38 ? 38 : y);
                 }

                 // zip the generated y values with the x values

                 var res = [];
                 for (var i = 0; i < dataF.length; ++i) {
                     res.push([i, dataF[i]]);
                 }

                 return res;
         }
         //lineAreaData = [{
         //    data: getRandomData(),
         //    lines: {
         //        fill: true
         //    }
         //}];
        //function getRandomData() {

        //    if (data.length) {
        //        data = data.slice(1);
        //    }

        //    while (data.length < maximum) {
        //        var previous = data.length ? data[data.length - 1] : 50;
        //        var y = previous + Math.random() * 10 - 5;
        //        data.push(y < 0 ? 0 : y > 100 ? 100 : y);
        //    }

        //    // zip the generated y values with the x values

        //    var res = [];
        //    for (var i = 0; i < data.length; ++i) {
        //        res.push([i, data[i]]);
        //    }

        //    return res;
        //}

        var data = {
            "US": 298,
            "SA": 200,
            "DE": 220,
            "FR": 540,
            "CN": 120,
            "AU": 760,
            "BR": 550,
            "IN": 200,
            "GB": 120
        };

        vm.data = data;

        var data1 = [
        [gd(2012, 1, 1), 7],
        [gd(2012, 1, 2), 6],
        [gd(2012, 1, 3), 4],
        [gd(2012, 1, 4), 8],
        [gd(2012, 1, 5), 9],
        [gd(2012, 1, 6), 7],
        [gd(2012, 1, 7), 5],
        [gd(2012, 1, 8), 4],
        [gd(2012, 1, 9), 7],
        [gd(2012, 1, 10), 8],
        [gd(2012, 1, 11), 9],
        [gd(2012, 1, 12), 6],
        [gd(2012, 1, 13), 4],
        [gd(2012, 1, 14), 5],
        [gd(2012, 1, 15), 11],
        [gd(2012, 1, 16), 8],
        [gd(2012, 1, 17), 8],
        [gd(2012, 1, 18), 11],
        [gd(2012, 1, 19), 11],
        [gd(2012, 1, 20), 6],
        [gd(2012, 1, 21), 6],
        [gd(2012, 1, 22), 8],
        [gd(2012, 1, 23), 11],
        [gd(2012, 1, 24), 13],
        [gd(2012, 1, 25), 7],
        [gd(2012, 1, 26), 9],
        [gd(2012, 1, 27), 9],
        [gd(2012, 1, 28), 8],
        [gd(2012, 1, 29), 5],
        [gd(2012, 1, 30), 8],
        [gd(2012, 1, 31), 25]
        ];

        var data2 = [
            [gd(2012, 1, 1), 800],
            [gd(2012, 1, 2), 500],
            [gd(2012, 1, 3), 600],
            [gd(2012, 1, 4), 700],
            [gd(2012, 1, 5), 500],
            [gd(2012, 1, 6), 456],
            [gd(2012, 1, 7), 800],
            [gd(2012, 1, 8), 589],
            [gd(2012, 1, 9), 467],
            [gd(2012, 1, 10), 876],
            [gd(2012, 1, 11), 689],
            [gd(2012, 1, 12), 700],
            [gd(2012, 1, 13), 500],
            [gd(2012, 1, 14), 600],
            [gd(2012, 1, 15), 700],
            [gd(2012, 1, 16), 786],
            [gd(2012, 1, 17), 345],
            [gd(2012, 1, 18), 888],
            [gd(2012, 1, 19), 888],
            [gd(2012, 1, 20), 888],
            [gd(2012, 1, 21), 987],
            [gd(2012, 1, 22), 444],
            [gd(2012, 1, 23), 999],
            [gd(2012, 1, 24), 567],
            [gd(2012, 1, 25), 786],
            [gd(2012, 1, 26), 666],
            [gd(2012, 1, 27), 888],
            [gd(2012, 1, 28), 900],
            [gd(2012, 1, 29), 178],
            [gd(2012, 1, 30), 555],
            [gd(2012, 1, 31), 993]
        ];


        var dataset = [
            {
                label: "Number of orders",
                grow: { stepMode: "linear" },
                data: data2,
                color: "#1ab394",
                bars: {
                    show: true,
                    align: "center",
                    barWidth: 24 * 60 * 60 * 600,
                    lineWidth: 0
                }

            },
            {
                label: "Payments",
                grow: { stepMode: "linear" },
                data: data1,
                yaxis: 2,
                color: "#1C84C6",
                lines: {
                    lineWidth: 1,
                    show: true,
                    fill: true,
                    fillColor: {
                        colors: [
                            {
                                opacity: 0.2
                            },
                            {
                                opacity: 0.2
                            }
                        ]
                    }
                }
            }
        ];


        var options = {
            grid: {
                hoverable: true,
                clickable: true,
                tickColor: "#d5d5d5",
                borderWidth: 0,
                color: '#d5d5d5'
            },
            colors: ["#1ab394", "#464f88"],
            tooltip: true,
            xaxis: {
                mode: "time",
                tickSize: [3, "day"],
                tickLength: 0,
                axisLabel: "Date",
                axisLabelUseCanvas: true,
                axisLabelFontSizePixels: 12,
                axisLabelFontFamily: 'Arial',
                axisLabelPadding: 10,
                color: "#d5d5d5"
            },
            yaxes: [
                {
                    position: "left",
                    max: 1070,
                    color: "#d5d5d5",
                    axisLabelUseCanvas: true,
                    axisLabelFontSizePixels: 12,
                    axisLabelFontFamily: 'Arial',
                    axisLabelPadding: 3
                },
                {
                    position: "right",
                    color: "#d5d5d5",
                    axisLabelUseCanvas: true,
                    axisLabelFontSizePixels: 12,
                    axisLabelFontFamily: ' Arial',
                    axisLabelPadding: 67
                }
            ],
            legend: {
                noColumns: 1,
                labelBoxBorderColor: "#d5d5d5",
                position: "nw"
            }

        };

        function gd(year, month, day) {
            return new Date(year, month - 1, day).getTime();
        }

        /**
         * Definition of variables
         * Flot chart
         */
        vm.flotData = dataset;
        vm.flotOptions = options;
        $scope.datayahoo = [];


        activate();

        function activate() {
            common.activateController([getMessageCount()], controllerId)
                .then(function () { log('Activated Dashboard_2') });//log('Activated Admin View');
        }

        $scope.$watch('yourItems', function (newVal, oldVal) {
            if (newVal !== oldVal) {
                // render charts
            }
        });
        var dataprev = [];
        function getQuoka() {
            var data = $scope.datayahoo;
            $scope.quoka = 0;
            if (data) {
                for (var i = 0; i < symbol.length; i++) {

                    if (i == 0) $scope.quoka += 0.45;
                    else {
                        $scope.quoka += (1 / data[i].Rate) * percentage[i];
                    }
                    
                }
                $scope.quoka = Math.round($scope.quoka * 1000) / 1000;
            }
        }

        function addStatus(data) {
            if (data) {
                if (dataprev.length === 0) dataprev = data;
                for (var i = 0; i < symbol.length; i++) {

                    if (dataprev[i].Rate > data[i].Rate) {
                        data[i]["status"] = 'text-danger';
                        data[i]["navigation"] = 'fa fa-play fa-rotate-90';
                    }
                    else if (dataprev[i].Rate < data[i].Rate) {
                        data[i]["status"] = 'text-navy';
                        data[i]["navigation"] = 'fa fa-play fa-rotate-270';
                    } else {
                        data[i]["status"] = '';
                        data[i]["navigation"] = 'fa fa-pause';
                    }
                }
                
                dataprev = data;
            }
            return data;
        }
        function getData() {

            if (dataF.length) {
                dataF = dataF.slice(1);
            }

            //while (dataF.length < maximum) {
            //    var previous = dataF.length ? dataF[dataF.length - 1] : 38;
            //    var y = previous + Math.random() * 10 - 5;
            //    dataF.push(y < 37 ? 37.1 : y > 38 ? 38 : y);
            //}

            // zip the generated y values with the x values
            dataF.push([dataF.length, $scope.quoka]);
            var res = [];
            for (var i = 0; i < dataF.length; ++i) {
                res.push([i, dataF[i][1]]);
            }
            //dataF = res;
            return res;
        }
        function getMessageCount() {
            return dataservice.getYData().then(function (data) {
                $scope.datayahoo = addStatus(data.data.query.results.rate);
                getQuoka();
                $scope.flotLineAreaData[0].data = getData();
                $scope.$digest();
            });
        }
    };


})();
