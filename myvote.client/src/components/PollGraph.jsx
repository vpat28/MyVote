import React, { useEffect, useRef, forwardRef, useImperativeHandle } from 'react';
import { Chart, registerables } from 'chart.js';

Chart.register(...registerables);

const PollGraph = forwardRef(({ poll }, ref) => {
    const chartRef = useRef(null);
    const chartInstanceRef = useRef(null);

    useImperativeHandle(ref, () => ({
        captureGraph: async () => {
            return new Promise((resolve) => {
                setTimeout(() => {
                    if (chartRef.current) {
                        resolve(chartRef.current.toDataURL('image/png'));
                    } else {
                        resolve(null);
                    }
                }, 1000); // Small delay to ensure the chart is fully rendered
            });
        }
    }));

    useEffect(() => {
        if (!chartRef.current || !poll || !poll.choices) return;

        const ctx = chartRef.current.getContext('2d');

        if (chartInstanceRef.current) {
            chartInstanceRef.current.destroy();
        }

        chartInstanceRef.current = new Chart(ctx, {
            type: 'pie',
            data: {
                labels: poll.choices.map(choice => choice.name),
                datasets: [{
                    label: '# of Votes',
                    data: poll.choices.map(choice => choice.numVotes),
                    backgroundColor: [
                        'rgba(255, 99, 132, 0.2)',
                        'rgba(54, 162, 235, 0.2)',
                        'rgba(255, 206, 86, 0.2)',
                        'rgba(75, 192, 192, 0.2)',
                        'rgba(153, 102, 255, 0.2)',
                        'rgba(255, 159, 64, 0.2)'
                    ],
                    borderColor: [
                        'rgba(255, 99, 132, 1)',
                        'rgba(54, 162, 235, 1)',
                        'rgba(255, 206, 86, 1)',
                        'rgba(75, 192, 192, 1)',
                        'rgba(153, 102, 255, 1)',
                        'rgba(255, 159, 64, 1)'
                    ],
                    borderWidth: 1
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false
            }
        });

        return () => {
            if (chartInstanceRef.current) {
                chartInstanceRef.current.destroy();
                chartInstanceRef.current = null;
            }
        };
    }, [poll]);

    return (
        <div className="chart-container">
            <canvas ref={chartRef}></canvas>
        </div>
    );
});

export default PollGraph;
